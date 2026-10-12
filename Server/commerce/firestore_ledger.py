"""Firestore-backed account ledger for Cloud Run.

The public behavior mirrors `ledger.Ledger` so the HTTP adapter and Unity client
can use either durable SQLite (local/tests) or Firestore (Cloud Run) without
changing protocol semantics.
"""
import hashlib
import json
from datetime import datetime, timezone

from google.cloud import firestore
from safe_identity import (NAME_KEY, profile_name, with_player_name, valid_player_name,
                           generate_player_name, fallback_player_name)

from ledger import (
    CONSUMABLES,
    COSMETICS,
    ITEMS,
    MAX_BALANCE,
    PACKS,
    ProfileConflict,
    validate_profile,
)


class FirestoreLedger:
    def __init__(self, project_id=None, namespace="dimensions-front", client=None):
        if not isinstance(namespace, str) or not namespace.strip():
            raise ValueError("Firestore namespace required")
        self.client = client or firestore.Client(project=project_id or None)
        prefix = namespace.strip().lower().replace("/", "-")
        self.accounts = self.client.collection(prefix + "-accounts")
        self.purchases = self.client.collection(prefix + "-purchase-tokens")
        self.name_claims = self.client.collection(prefix + "-player-names")

    def _reserve_name(self, tx, account, data, preferred=None):
        old = profile_name(data['profile'])
        # The submitted profile never chooses the account name.
        # Preserve only an identity already stored by the account server.
        candidate = old if valid_player_name(old) else generate_player_name()
        for _ in range(64):
            ref = self.name_claims.document(candidate)
            snap = ref.get(transaction=tx)
            owner = (snap.to_dict() or {}).get('account') if snap.exists else None
            if owner in (None, account):
                old_ref = None
                if valid_player_name(old) and old != candidate:
                    previous = self.name_claims.document(old)
                    previous_snap = previous.get(transaction=tx)
                    if previous_snap.exists and (previous_snap.to_dict() or {}).get('account') == account:
                        old_ref = previous
                return candidate, ref, old_ref
            candidate = generate_player_name(candidate)
        raise ValueError('Name allocation unavailable; try again')

    @staticmethod
    def _write_name_claim(tx, account, ref, previous):
        tx.set(ref, {'account': account})
        if previous is not None:
            tx.delete(previous)

    @staticmethod
    def _empty():
        return {
            "balance": 0,
            "revision": 0,
            "profileRevision": 0,
            "profile": [],
            "owned": [],
        }

    @classmethod
    def _data(cls, snapshot):
        data = snapshot.to_dict() if snapshot is not None and snapshot.exists else cls._empty()
        if not isinstance(data, dict):
            data = cls._empty()
        return {
            "balance": int(data.get("balance", 0)),
            "revision": int(data.get("revision", 0)),
            "profileRevision": int(data.get("profileRevision", 0)),
            "profile": data.get("profile") if isinstance(data.get("profile"), list) else [],
            "owned": data.get("owned") if isinstance(data.get("owned"), list) else [],
        }

    @staticmethod
    def _snapshot(account, data, operation_id=None):
        owned = list(dict.fromkeys(s for s in data.get("owned", []) if isinstance(s, str)))
        result = {
            "profileRevision": max(0, int(data.get("profileRevision", 0))),
            "profile": data.get("profile", []),
            "balance": max(0, int(data.get("balance", 0))),
            "revision": max(0, int(data.get("revision", 0))),
            "voidfall": "voidfall" in owned,
            "avatars": [int(s[7:]) for s in owned if s.startswith("avatar_") and s[7:].isdigit()],
            "owned": owned,
        }
        if operation_id:
            result["operationId"] = operation_id
        return result

    def wallet(self, account):
        if not account:
            raise ValueError("Authenticated account required")
        ref = self.accounts.document(account)
        transaction = self.client.transaction()

        @firestore.transactional
        def load(tx):
            snap = ref.get(transaction=tx)
            data = self._data(snap)
            name, claim, previous = self._reserve_name(tx, account, data)
            clean = with_player_name(data['profile'], name)
            changed = clean != data['profile']
            if changed:
                data['profileRevision'] += int(bool(data['profile']))
                data['profile'] = clean
            self._write_name_claim(tx, account, claim, previous)
            if not snap.exists or changed:
                tx.set(ref, data)
            return data

        return self._snapshot(account, load(transaction))

    def fulfill(self, account, product, token, verified):
        if product not in PACKS or not token:
            raise ValueError("Unknown product or empty token")
        if verified.get("purchaseState") != 0:
            raise ValueError("Payment not PURCHASED")
        if verified.get("obfuscatedExternalAccountId") != account:
            raise ValueError("Purchase account mismatch")
        quantity = verified.get("quantity", 1)
        if type(quantity) is not int or not 1 <= quantity <= 100:
            raise ValueError("Invalid quantity")
        self.wallet(account)
        token_id = hashlib.sha256(token.encode("utf-8")).hexdigest()
        purchase_ref = self.purchases.document(token_id)
        account_ref = self.accounts.document(account)
        transaction = self.client.transaction()

        @firestore.transactional
        def apply(tx):
            purchase = purchase_ref.get(transaction=tx)
            account_snap = account_ref.get(transaction=tx)
            data = self._data(account_snap)
            if purchase.exists:
                previous = purchase.to_dict() or {}
                if (previous.get("account"), previous.get("product"), previous.get("quantity")) != (account, product, quantity):
                    raise ValueError("Purchase token already bound")
                return data
            # A previously credited receipt may already be consumed at Google.
            # Only an unknown consumed token needs manual reconciliation.
            if verified.get("consumptionState", 0) != 0:
                raise ValueError("Unknown consumed purchase requires reconciliation")
            amount = PACKS[product] * quantity
            if data["balance"] + amount > MAX_BALANCE:
                raise ValueError("Wallet capacity exceeded")
            data["balance"] += amount
            data["revision"] += 1
            tx.set(account_ref, data)
            tx.set(purchase_ref, {
                "account": account,
                "product": product,
                "quantity": quantity,
                "credited": amount,
                "createdAt": datetime.now(timezone.utc),
            })
            return data

        return self._snapshot(account, apply(transaction))

    def claim(self, account):
        raise ValueError("Legacy local shard claims disabled; update the client")

    def acknowledge_claim(self, account, claim_id):
        raise ValueError("Legacy local shard claims disabled; update the client")

    @staticmethod
    def validate_operation(operation_id):
        if not isinstance(operation_id, str) or len(operation_id) != 32 or any(c not in "0123456789abcdef" for c in operation_id):
            raise ValueError("Invalid operation ID")

    def save_profile(self, account, entries, expected_revision):
        entries = validate_profile(entries)
        if type(expected_revision) is not int or expected_revision < 0:
            raise ValueError("Invalid profile revision")
        account_ref = self.accounts.document(account)
        transaction = self.client.transaction()

        @firestore.transactional
        def save(tx):
            snap = account_ref.get(transaction=tx)
            data = self._data(snap)
            if data["profileRevision"] != expected_revision:
                raise ProfileConflict()
            by_key = {e["key"]: e for e in entries}
            saved = {e["key"]: e for e in data["profile"]}
            name, claim, previous = self._reserve_name(tx, account, data, profile_name(entries))
            by_key[NAME_KEY] = {'key': NAME_KEY, 'kind': 1, 'text': name}
            daily_day, daily_index = "DF_LoginStreakDay_v1", "DF_LoginStreakIndex_v1"
            if daily_day not in by_key and daily_index not in by_key:
                for key in (daily_day, daily_index):
                    if key in saved:
                        by_key[key] = saved[key]
            if (daily_day in by_key) != (daily_index in by_key):
                raise ValueError("Incomplete login streak")
            if daily_day in saved and daily_day in by_key:
                old_day, new_day = saved[daily_day]["number"], by_key[daily_day]["number"]
                if new_day < old_day:
                    raise ValueError("Login day cannot move backwards")
                if new_day == old_day and by_key[daily_index] != saved.get(daily_index):
                    raise ValueError("Login streak already recorded for this day")
            for sku in data["owned"]:
                for write in ITEMS.get(sku, {}).get("writes", []):
                    if "Owned" in write["key"] or write["key"] == "DF_BP_BETA01_Premium":
                        by_key[write["key"]] = write
            profile = sorted(by_key.values(), key=lambda e: e["key"])
            self._write_name_claim(tx, account, claim, previous)
            if json.dumps(profile, separators=(",", ":"), sort_keys=True) != json.dumps(data["profile"], separators=(",", ":"), sort_keys=True):
                data["profile"] = profile
                data["profileRevision"] += 1
                tx.set(account_ref, data)
            elif not snap.exists:
                tx.set(account_ref, data)
            return data

        return self._snapshot(account, save(transaction))

    def purchase(self, account, sku, operation_id, expected_revision):
        self.validate_operation(operation_id)
        if not isinstance(sku, str) or sku not in ITEMS:
            raise ValueError("Unknown item")
        if ITEMS[sku].get("available", True) is not True:
            raise ValueError("Item is not released")
        if type(expected_revision) is not int or expected_revision < 0:
            raise ValueError("Invalid profile revision")
        self.wallet(account)
        account_ref = self.accounts.document(account)
        operation_ref = account_ref.collection("operations").document(operation_id)
        transaction = self.client.transaction()

        @firestore.transactional
        def buy(tx):
            operation = operation_ref.get(transaction=tx)
            snap = account_ref.get(transaction=tx)
            data = self._data(snap)
            if operation.exists:
                previous = operation.to_dict() or {}
                if previous.get("sku") != sku or previous.get("kind") != "buy":
                    raise ValueError("Operation already bound")
                return self._snapshot(account, data, operation_id)
            if data["profileRevision"] != expected_revision:
                raise ProfileConflict()

            entries = {e["key"]: e for e in data["profile"]}
            definition = ITEMS[sku]
            writes = definition.get("writes", [])
            already_owned = sku not in CONSUMABLES and (
                sku in data["owned"] or
                (writes and entries.get(writes[0]["key"], {}).get("number") == 1)
            )
            contribution = 0
            changed = False
            if not already_owned:
                earned = entries.get("DF_Shards", {}).get("number", 0)
                contribution = min(earned, COSMETICS[sku])
                paid = COSMETICS[sku] - contribution
                if data["balance"] < paid:
                    raise ValueError("Insufficient shards")
                data["balance"] -= paid
                data["revision"] += 1
                entries["DF_Shards"] = {"key": "DF_Shards", "kind": 0, "number": earned - contribution}
                increment_key = definition.get("incrementKey")
                if sku in CONSUMABLES and increment_key:
                    old = entries.get(increment_key, {}).get("number", 0)
                    if old >= MAX_BALANCE:
                        raise ValueError("Inventory capacity exceeded")
                    entries[increment_key] = {"key": increment_key, "kind": 0, "number": old + 1}
                else:
                    for write in writes:
                        entries[write["key"]] = write
                    if sku not in data["owned"]:
                        data["owned"].append(sku)
                changed = True
            else:
                for write in writes:
                    if ("Owned" in write["key"] or write["key"] == "DF_BP_BETA01_Premium") and entries.get(write["key"]) != write:
                        entries[write["key"]] = write
                        changed = True

            if changed:
                data["profile"] = list(entries.values())
                data["profileRevision"] += 1
                tx.set(account_ref, data)
            elif not snap.exists:
                tx.set(account_ref, data)
            tx.set(operation_ref, {
                "sku": sku,
                "localContribution": contribution,
                "kind": "buy",
                "createdAt": datetime.now(timezone.utc),
            })
            return self._snapshot(account, data, operation_id)

        return buy(transaction)

    def buy(self, account, sku):
        if sku not in COSMETICS or sku in CONSUMABLES:
            raise ValueError("Unknown cosmetic")
        if ITEMS[sku].get("available", True) is not True:
            raise ValueError("Item is not released")
        self.wallet(account)
        account_ref = self.accounts.document(account)
        transaction = self.client.transaction()

        @firestore.transactional
        def buy_once(tx):
            snap = account_ref.get(transaction=tx)
            data = self._data(snap)
            if sku in data["owned"]:
                return data
            price = COSMETICS[sku]
            if data["balance"] < price:
                raise ValueError("Insufficient shards")
            data["balance"] -= price
            data["revision"] += 1
            data["owned"].append(sku)
            tx.set(account_ref, data)
            return data

        return self._snapshot(account, buy_once(transaction))
