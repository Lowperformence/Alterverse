# Timmy release gate

Compile the production `HeroUnlockCatalog` and `PlayerHeroSelection` with isolated profile, wallet and transaction fixtures:

```sh
python Tools/Shop/TimmyReleaseRegression/run.py --mono-root /path/to/mono --roslyn /path/to/csc.exe
```

122 assertions cover the 1,900 shard listing, locked purchase and deployment across online/offline wallets, insufficient/exact/excess balances, old ownership flags, no currency or entitlement mutations, and unchanged free/Traveler/Nerok availability and prices.

Server checks:

```sh
cd Server/commerce
python -m unittest test_ledger test_application test_profile test_purchase_restoration
```

The server suite covers both purchase endpoints, SQLite debit rejection, Firestore rejection before database access, and existing purchase/profile restoration. Firestore tests use isolated transaction fixtures. These checks do not render Unity buttons or contact live billing/database services.
