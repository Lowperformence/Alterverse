import { http } from '@google-cloud/functions-framework';
import { Firestore, FieldValue } from '@google-cloud/firestore';
import { GoogleAuth } from 'google-auth-library';
import { createHash } from 'node:crypto';
import { createRemoteJWKSet, jwtVerify } from 'jose';

const PACKAGE_NAME = 'com.lowperformence.dimensionsfront';
const UNITY_PROJECT_ID = '57bb61ab-1371-4b7a-a996-5a932e351c03';
const UNITY_ENVIRONMENT = 'production';
const SERVER_REVISION = '2026-10-22.2';
const MAX_BALANCE = 2147483647;

const PRODUCT_SHARDS = Object.freeze({
  dimensions_shards_500: 500,
  dimensions_shards_1200: 1200,
  dimensions_shards_5500: 5500,
  dimensions_shards_12000: 12000
});

const SUBSCRIPTION_ID = 'dimensions_shards_monthly';
const SUBSCRIPTION_BASE_PLAN = 'monthly';
const SUBSCRIPTION_SHARDS = 1200;
const SUBSCRIPTION_ACTIVE_STATES = new Set([
  'SUBSCRIPTION_STATE_ACTIVE',
  'SUBSCRIPTION_STATE_CANCELED',
  'SUBSCRIPTION_STATE_IN_GRACE_PERIOD'
]);
const GOOGLE_JWKS = createRemoteJWKSet(
  new URL('https://www.googleapis.com/oauth2/v3/certs')
);

const w = (key, number = 1) => ({ key, kind: 0, number });
const item = (price, writes = [], consumable = false) => ({
  price,
  writes,
  consumable
});

const ITEMS = {
  voidfall: item(2000, [w('DF_SkinOwned_Voidfall')]),
  beta_supporter_wings: item(8000, [w('DF_SkinOwned_BetaSupporterWings')]),
  plague_wings: item(4000, [w('DF_SkinOwned_PlagueWings')]),
  deluxe_wings: item(20000, [w('DF_SkinOwned_DeluxeWings')]),
  hero_traveler: item(1500, [w('DF_HeroOwned_Traveler')]),
  hero_nerok: item(2200, [w('DF_HeroOwned_Nerok')]),
  battlepass_beta01: item(1000, [w('DF_BP_BETA01_Premium')]),
  shield: item(100, [], true),

  arena_GreenPlanet: item(800, [w('DF_ArenaOwned_GreenPlanet')]),
  arena_Noxara: item(1000, [w('DF_ArenaOwned_Noxara')]),
  arena_Earth: item(500, [w('DF_ArenaOwned_Earth')]),
  arena_Pyraxis: item(800, [w('DF_ArenaOwned_Pyraxis')]),
  arena_Dark00: item(1800, [w('DF_ArenaOwned_Dark00')]),
  arena_DiamondCore: item(1200, [w('DF_ArenaOwned_DiamondCore')])
};

const avatarPrices = new Map([
  [10,450],
  [11,450],
  [12,600],
  [13,750],
  [14,750],
  [15,900],
  [17,450],
  [18,450],
  [19,450],
  [20,450],
  [21,450],
  [22,450],
  [23,450],
  [24,450],
  [25,450],
  [26,450],
  [27,500],
  [28,500],
  [29,500],
  [30,500],
  [31,500]
]);

for (const [id, price] of avatarPrices) {
  ITEMS[`avatar_${id}`] = item(price, [
    w(`DF_AvatarOwned_${id}`),
    w('DF_Avatar', id)
  ]);
}

for (let hero = 0; hero <= 7; hero++) {
  const baseWrites = [
    w(`DF_WeaponSkinOwnedItem_${hero}_0`),
    w(`DF_WeaponSkinSelected_${hero}`, 0),
    w(`DF_WeaponSkinEquipped_${hero}`, 1),
    w(`DF_WeaponSkinOwned_${hero}`, 1)
  ];

  if (hero === 0) {
    baseWrites.push(
      w('DF_WeaponSkinEquippedVariant_0', 0)
    );
  }

  ITEMS[`weapon_${hero}_0`] = item(
    500,
    baseWrites
  );

  ITEMS[`weapon_${hero}_1`] = item(
    500,
    [
      w(`DF_WeaponSkinOwnedItem_${hero}_1`),
      w(`DF_WeaponSkinSelected_${hero}`, 1),
      w(`DF_WeaponSkinEquipped_${hero}`, 1)
    ]
  );
}

const UNITY_JWKS = createRemoteJWKSet(
  new URL(
    'https://player-auth.services.api.unity.com/.well-known/jwks.json'
  )
);

const db = new Firestore();

const googleAuth = new GoogleAuth({
  scopes: [
    'https://www.googleapis.com/auth/androidpublisher'
  ]
});

let googleClientPromise;

class HttpError extends Error {
  constructor(status, code) {
    super(code);
    this.status = status;
    this.code = code;
  }
}

function sha256(value) {
  return createHash('sha256')
    .update(value)
    .digest('hex');
}

function normalizeAudience(aud) {
  if (Array.isArray(aud)) {
    return aud.map(String);
  }

  if (typeof aud === 'string') {
    return [aud];
  }

  return [];
}

async function authenticateUnity(req) {
  const header = String(
    req.get('authorization') || ''
  );

  if (!header.startsWith('Bearer ')) {
    throw new HttpError(
      401,
      'authentication_required'
    );
  }

  const token = header.slice(7).trim();

  if (!token) {
    throw new HttpError(
      401,
      'authentication_required'
    );
  }

  try {
    const { payload } = await jwtVerify(
      token,
      UNITY_JWKS,
      {
        algorithms: ['RS256']
      }
    );

    const audience =
      normalizeAudience(payload.aud);

    if (
      payload.project_id !==
      UNITY_PROJECT_ID
    ) {
      throw new Error(
        'project mismatch'
      );
    }

    if (
      !audience.includes(
        `upid:${UNITY_PROJECT_ID}`
      )
    ) {
      throw new Error(
        'project audience mismatch'
      );
    }

    if (
      !audience.includes(
        `envName:${UNITY_ENVIRONMENT}`
      )
    ) {
      throw new Error(
        'environment mismatch'
      );
    }

    const playerId =
      String(payload.sub || '');

    if (
      !playerId ||
      playerId.length > 128
    ) {
      throw new Error(
        'invalid player id'
      );
    }

    return {
      playerId,
      accountHash: sha256(playerId)
    };
  } catch {
    throw new HttpError(
      401,
      'invalid_authentication'
    );
  }
}

function playerRef(accountHash) {
  return db
    .collection('players')
    .doc(accountHash);
}

function defaultPlayer() {
  return {
    balance: 0,
    revision: 0,
    profileRevision: 0,
    profile: [],
    owned: []
  };
}

function normalizePlayer(data) {
  const d =
    data &&
    typeof data === 'object'
      ? data
      : {};

  return {
    balance:
      Number.isInteger(d.balance) &&
      d.balance >= 0
        ? d.balance
        : 0,

    revision:
      Number.isInteger(d.revision) &&
      d.revision >= 0
        ? d.revision
        : 0,

    profileRevision:
      Number.isInteger(d.profileRevision) &&
      d.profileRevision >= 0
        ? d.profileRevision
        : 0,

    profile:
      Array.isArray(d.profile)
        ? d.profile
        : [],

    owned:
      Array.isArray(d.owned)
        ? d.owned.filter(
            x => typeof x === 'string'
          )
        : []
  };
}

function walletSnapshot(
  accountHash,
  data,
  operationId = ''
) {
  const p = normalizePlayer(data);

  const owned =
    [...new Set(p.owned)].sort();

  return {
    revision: p.revision,
    profileRevision: p.profileRevision,
    balance: p.balance,

    voidfall:
      owned.includes('voidfall'),

    billingReady: true,
    subscriptionsReady: false,
    subscriptionActive: false,

    avatars: owned
      .filter(
        x => /^avatar_\d+$/.test(x)
      )
      .map(
        x => Number(x.substring(7))
      )
      .filter(Number.isInteger)
      .sort((a, b) => a - b),

    owned,
    profile: p.profile,
    operationId,
    accountHash
  };
}

const RANKING_PAGE_SIZE = 8;
const MAX_RANKING_PLAYERS = 1000;
const MAX_PLAYER_XP = 100 * 999 + 2 * 999 * 999 + Math.floor(999 ** 3 / 250);
let rankingCache;
let rankingRefresh;

function displayName(value) {
  return typeof value === 'string' && value.length > 0 && value.length <= 32 &&
    Buffer.byteLength(value, 'utf8') <= 60 && value === value.trim() &&
    value.normalize('NFC') === value &&
    !/[<>\p{Cc}\p{Cf}\p{Cs}\p{Co}\p{Cn}\p{Zl}\p{Zp}]/u.test(value);
}

function publicRankingPlayer(accountHash, data) {
  const entries = new Map();
  for (const entry of Array.isArray(data?.profile) ? data.profile : []) {
    if (entry && typeof entry.key === 'string') entries.set(entry.key, entry);
  }
  // Public directory IDs never contain the Unity subject or commerce account ID.
  const playerId = sha256('dimensions-front/social-player/v1/' + accountHash).slice(0, 20);
  const alias = entries.get('DF_PlayGamesDisplayName');
  const storedName = entries.get('DF_PlayerName');
  const name = alias?.kind === 1 && displayName(alias.text) ? alias.text :
    storedName?.kind === 1 && displayName(storedName.text) ? storedName.text :
    'Pilot' + (BigInt('0x' + playerId) % 10000000n).toString().padStart(7, '0');
  const xpEntry = entries.get('DF_AccountXP');
  const rawXp = xpEntry?.kind === 1 ? xpEntry.text : null;
  let xp = 0;
  if (typeof rawXp === 'string' && rawXp.length <= 20 && /^[+-]?\d+$/.test(rawXp.trim())) {
    const parsed = BigInt(rawXp.trim());
    xp = parsed <= 0n ? 0 : parsed >= BigInt(MAX_PLAYER_XP) ? MAX_PLAYER_XP : Number(parsed);
  }
  let lo = 1, hi = 1000;
  while (lo < hi) {
    const mid = Math.floor((lo + hi + 1) / 2), n = mid - 1;
    if (100 * n + 2 * n * n + Math.floor(n ** 3 / 250) <= xp) lo = mid;
    else hi = mid - 1;
  }
  const avatarEntry = entries.get('DF_Avatar');
  const avatar = avatarEntry?.kind === 0 && Number.isInteger(avatarEntry.number) &&
    avatarEntry.number >= 0 && avatarEntry.number <= 1000 ? avatarEntry.number : 0;
  return { playerId, name, level: lo, xp, avatar };
}

function compareRankingPlayers(a, b) {
  return b.level - a.level || b.xp - a.xp || (a.playerId < b.playerId ? -1 : a.playerId > b.playerId ? 1 : 0);
}

async function rankingDirectory() {
  if (rankingCache && Date.now() < rankingCache.expiresAt) return rankingCache.players;
  if (rankingRefresh) return rankingRefresh;
  rankingRefresh = (async () => {
    const players = [];
    let count = 0;
    // This legacy service stores XP inside a profile array. Stream only profiles
    // and bound the scan; never present a truncated directory as a global rank.
    const query = db.collection('players').select('profile').limit(MAX_RANKING_PLAYERS + 1);
    for await (const document of query.stream()) {
      if (++count > MAX_RANKING_PLAYERS) throw new HttpError(503, 'ranking_directory_limit');
      if (/^[a-f0-9]{64}$/.test(document.id)) players.push(publicRankingPlayer(document.id, document.data()));
    }
    rankingCache = { players, expiresAt: Date.now() + 15000 };
    return players;
  })();
  try { return await rankingRefresh; }
  finally { rankingRefresh = null; }
}

async function readPlayerLevels(accountHash, body) {
  const offset = body.offset ?? 0;
  if (!Number.isInteger(offset) || offset < 0 || offset > 10000) {
    throw new HttpError(400, 'invalid_ranking_offset');
  }
  const snapshot = await playerRef(accountHash).get();
  const me = publicRankingPlayer(accountHash, snapshot.exists ? snapshot.data() : defaultPlayer());
  const players = (await rankingDirectory()).filter(player => player.playerId !== me.playerId);
  // Include the caller's latest committed save even while the directory is cached.
  players.push(me);
  players.sort(compareRankingPlayers);
  const selfRank = players.findIndex(player => player.playerId === me.playerId) + 1;
  return {
    me, selfRank, total: players.length, offset,
    players: players.slice(offset, offset + RANKING_PAGE_SIZE).map((player, index) =>
      ({ ...player, rank: offset + index + 1 }))
  };
}

// Must match the reviewed SafeIdentityCatalog bundled with the Android client.
// This internal account name is separate from the Google Play display alias.
const PLAYER_NAME_KEY = 'DF_PlayerName';
const PLAYER_NAME_ROOTS = [
  'Solar', 'Lunar', 'Nova', 'Star', 'Comet', 'Orbit', 'Polar', 'Dawn',
  'Dusk', 'Azure', 'Ember', 'Mist', 'Frost', 'Cloud', 'Ocean', 'Coral'
].flatMap(prefix => ['Fox', 'Owl', 'Lynx', 'Hawk', 'Seal', 'Bear', 'Wolf', 'Puma']
  .map(animal => prefix + animal));
const PLAYER_NAME_ROOT_SET = new Set(PLAYER_NAME_ROOTS);

function validPlayerName(value) {
  return typeof value === 'string' && /^[A-Za-z]+[0-9]{7}$/.test(value) &&
    PLAYER_NAME_ROOT_SET.has(value.slice(0, -7));
}

async function reservePlayerName(tx, accountHash, profile) {
  const saved = profile.find(entry => entry?.key === PLAYER_NAME_KEY &&
    entry.kind === 1 && validPlayerName(entry.text));
  for (let attempt = 0; attempt < 64; attempt++) {
    // Domain-separated candidates reveal neither the Unity ID nor its account hash.
    const digest = BigInt('0x' + sha256('dimensions-front/player-name/v1/' +
      accountHash + '/' + attempt));
    const name = attempt === 0 && saved ? saved.text :
      PLAYER_NAME_ROOTS[Number(digest % BigInt(PLAYER_NAME_ROOTS.length))] +
      String(digest / BigInt(PLAYER_NAME_ROOTS.length) % 10000000n).padStart(7, '0');
    const ref = db.collection('playerNameClaims').doc(name);
    const claim = await tx.get(ref);
    if (!claim.exists || claim.data()?.accountHash === accountHash) {
      return { name, ref, needsClaim: !claim.exists };
    }
  }
  throw new HttpError(503, 'player_name_allocation_failed');
}

async function readWallet(accountHash) {
  const ref = playerRef(accountHash);
  return db.runTransaction(async tx => {
    const snap = await tx.get(ref);
    const current = normalizePlayer(snap.exists ? snap.data() : defaultPlayer());
    const identity = await reservePlayerName(tx, accountHash, current.profile);
    const names = current.profile.filter(entry => entry?.key === PLAYER_NAME_KEY);
    const changed = names.length !== 1 || names[0].kind !== 1 ||
      names[0].text !== identity.name;
    const next = { ...current };
    if (changed) {
      next.profile = current.profile.filter(entry => entry?.key !== PLAYER_NAME_KEY);
      next.profile.push({ key: PLAYER_NAME_KEY, kind: 1, text: identity.name });
      next.profile.sort((a, b) => String(a?.key).localeCompare(String(b?.key)));
      // Keep an empty initial profile at revision 0 so the existing client can
      // import local progress. Migrations of existing saves invalidate stale writes.
      next.profileRevision += current.profile.length > 0 || current.profileRevision > 0 ? 1 : 0;
    }
    // Every transaction read above completes before any write is scheduled.
    if (identity.needsClaim) tx.set(identity.ref, { accountHash });
    if (!snap.exists || changed) {
      tx.set(ref, {
        ...(!snap.exists ? next : { profile: next.profile, profileRevision: next.profileRevision }),
        updatedAt: FieldValue.serverTimestamp()
      }, { merge: true });
    }
    return walletSnapshot(accountHash, next);
  });
}

function isProfileKey(key) {
  return (
    typeof key === 'string' &&
    key.startsWith('DF_') &&
    key.length <= 160 &&
    !key.startsWith(
      'DF_ProfileTransaction'
    ) &&
    !key.startsWith(
      'DF_Cloud'
    ) &&
    !key.startsWith(
      'DF_Commerce'
    ) &&
    !key.startsWith(
      'DF_ShardClaim_'
    ) &&
    !key.startsWith(
      'DF_AccountProfile'
    )
  );
}

function berlinTodayNumber() {
  const parts =
    new Intl.DateTimeFormat(
      'en-US',
      {
        timeZone: 'Europe/Berlin',
        year: 'numeric',
        month: '2-digit',
        day: '2-digit'
      }
    ).formatToParts(new Date());

  const pick = type =>
    Number(
      parts.find(
        p => p.type === type
      )?.value || 0
    );

  return (
    pick('year') * 10000 +
    pick('month') * 100 +
    pick('day')
  );
}

function validCalendarNumber(value) {
  if (
    !Number.isInteger(value) ||
    value < 10101 ||
    value > 99991231
  ) {
    return false;
  }

  const year =
    Math.floor(value / 10000);

  const month =
    Math.floor(value / 100) % 100;

  const day =
    value % 100;

  const date =
    new Date(
      Date.UTC(
        year,
        month - 1,
        day
      )
    );

  return (
    date.getUTCFullYear() === year &&
    date.getUTCMonth() ===
      month - 1 &&
    date.getUTCDate() === day
  );
}

function validateProfile(entries) {
  if (
    !Array.isArray(entries) ||
    entries.length > 2048
  ) {
    throw new HttpError(
      400,
      'invalid_profile'
    );
  }

  const clean = [];
  const seen = new Set();

  for (const entry of entries) {
    if (
      !entry ||
      typeof entry !== 'object'
    ) {
      throw new HttpError(
        400,
        'invalid_profile'
      );
    }

    const key = entry.key;
    const kind = entry.kind;

    if (
      !isProfileKey(key) ||
      seen.has(key) ||
      !Number.isInteger(kind)
    ) {
      throw new HttpError(
        400,
        'invalid_profile'
      );
    }

    seen.add(key);

    if (kind === 0) {
      const number =
        entry.number;

      if (
        !Number.isInteger(number) ||
        number < -2147483648 ||
        number > MAX_BALANCE
      ) {
        throw new HttpError(
          400,
          'invalid_profile'
        );
      }

      if (
        key === 'DF_Shards' &&
        number < 0
      ) {
        throw new HttpError(
          400,
          'invalid_profile'
        );
      }

      clean.push({
        key,
        kind,
        number
      });
    } else if (kind === 1) {
      const text =
        entry.text ?? '';

      if (
        typeof text !== 'string' ||
        text.length > 65536
      ) {
        throw new HttpError(
          400,
          'invalid_profile'
        );
      }

      clean.push({
        key,
        kind,
        text
      });
    } else if (kind === 2) {
      const real =
        entry.real;

      if (
        typeof real !== 'number' ||
        !Number.isFinite(real)
      ) {
        throw new HttpError(
          400,
          'invalid_profile'
        );
      }

      clean.push({
        key,
        kind,
        real
      });
    } else {
      throw new HttpError(
        400,
        'invalid_profile'
      );
    }
  }

  const byKey =
    new Map(
      clean.map(
        e => [e.key, e]
      )
    );

  const dayKey =
    'DF_LoginStreakDay_v1';

  const indexKey =
    'DF_LoginStreakIndex_v1';

  const winsKey =
    'DF_DailyQuestWonMaps_v1';

  if (byKey.has(indexKey)) {
    const e =
      byKey.get(indexKey);

    if (
      e.kind !== 0 ||
      e.number < 1 ||
      e.number > 7
    ) {
      throw new HttpError(
        400,
        'invalid_profile'
      );
    }
  }

  if (byKey.has(winsKey)) {
    const e =
      byKey.get(winsKey);

    if (
      e.kind !== 0 ||
      e.number < 0 ||
      e.number > (1 << 11) - 1
    ) {
      throw new HttpError(
        400,
        'invalid_profile'
      );
    }
  }

  if (byKey.has(dayKey)) {
    const e =
      byKey.get(dayKey);

    if (
      e.kind !== 0 ||
      !validCalendarNumber(
        e.number
      ) ||
      e.number >
        berlinTodayNumber()
    ) {
      throw new HttpError(
        400,
        'invalid_profile'
      );
    }
  }

  return clean.sort(
    (a, b) =>
      a.key.localeCompare(b.key)
  );
}

function sameEntry(a, b) {
  return (
    JSON.stringify(a) ===
    JSON.stringify(b)
  );
}

function profileMap(entries) {
  return new Map(
    (
      Array.isArray(entries)
        ? entries
        : []
    ).map(
      e => [
        e.key,
        { ...e }
      ]
    )
  );
}

function enforceOwnedWrites(
  map,
  owned
) {
  for (const sku of owned) {
    const def = ITEMS[sku];

    if (!def) continue;

    for (
      const write of
      def.writes
    ) {
      if (
        write.key.includes(
          'Owned'
        ) ||
        write.key ===
          'DF_BP_BETA01_Premium'
      ) {
        map.set(
          write.key,
          { ...write }
        );
      }
    }
  }
}

function sortedProfileFromMap(map) {
  return [...map.values()]
    .sort(
      (a, b) =>
        a.key.localeCompare(b.key)
    );
}

async function saveProfile(
  accountHash,
  entries,
  expectedRevision
) {
  const validated =
    validateProfile(entries);

  if (
    !Number.isInteger(
      expectedRevision
    ) ||
    expectedRevision < 0
  ) {
    throw new HttpError(
      400,
      'invalid_profile_revision'
    );
  }

  const ref =
    playerRef(accountHash);

  return db.runTransaction(
    async tx => {
      const snap =
        await tx.get(ref);

      const current =
        normalizePlayer(
          snap.exists
            ? snap.data()
            : defaultPlayer()
        );

      if (
        current.profileRevision !==
        expectedRevision
      ) {
        throw new HttpError(
          409,
          'profile_conflict'
        );
      }

      const submitted =
        profileMap(validated);

      // A client save may omit or spoof the internal name. Preserve only the
      // server's existing identity (or allocate one); never trust that field.
      const identity = await reservePlayerName(tx, accountHash, current.profile);
      submitted.set(PLAYER_NAME_KEY, { key: PLAYER_NAME_KEY, kind: 1, text: identity.name });

      const saved =
        profileMap(
          current.profile
        );

      const dayKey =
        'DF_LoginStreakDay_v1';

      const indexKey =
        'DF_LoginStreakIndex_v1';

      if (
        !submitted.has(dayKey) &&
        !submitted.has(indexKey)
      ) {
        if (saved.has(dayKey)) {
          submitted.set(
            dayKey,
            saved.get(dayKey)
          );
        }

        if (saved.has(indexKey)) {
          submitted.set(
            indexKey,
            saved.get(indexKey)
          );
        }
      }

      if (
        submitted.has(dayKey) !==
        submitted.has(indexKey)
      ) {
        throw new HttpError(
          400,
          'incomplete_login_streak'
        );
      }

      if (
        saved.has(dayKey) &&
        submitted.has(dayKey)
      ) {
        const oldDay =
          saved.get(dayKey).number;

        const newDay =
          submitted.get(dayKey).number;

        if (newDay < oldDay) {
          throw new HttpError(
            400,
            'login_day_backwards'
          );
        }

        if (
          newDay === oldDay &&
          !sameEntry(
            submitted.get(indexKey),
            saved.get(indexKey)
          )
        ) {
          throw new HttpError(
            400,
            'login_streak_already_recorded'
          );
        }
      }

      enforceOwnedWrites(
        submitted,
        current.owned
      );

      const nextProfile =
        sortedProfileFromMap(
          submitted
        );

      const changed =
        JSON.stringify(
          nextProfile
        ) !==
        JSON.stringify(
          current.profile
        );

      const next = {
        ...current,
        profile: nextProfile,

        profileRevision:
          current.profileRevision +
          (changed ? 1 : 0)
      };

      if (identity.needsClaim) tx.set(identity.ref, { accountHash });

      if (
        !snap.exists ||
        changed
      ) {
        tx.set(
          ref,
          {
            ...next,
            updatedAt:
              FieldValue
                .serverTimestamp()
          },
          { merge: true }
        );
      }

      return walletSnapshot(
        accountHash,
        next
      );
    }
  );
}

function validOperationId(value) {
  return (
    typeof value === 'string' &&
    /^[0-9a-f]{32}$/.test(value)
  );
}

async function spendWallet(
  accountHash,
  sku,
  operationId,
  expectedProfileRevision
) {
  if (
    !validOperationId(
      operationId
    )
  ) {
    throw new HttpError(
      400,
      'invalid_operation_id'
    );
  }

  const def =
    ITEMS[sku];

  if (!def) {
    throw new HttpError(
      400,
      'unknown_item'
    );
  }

  if (
    !Number.isInteger(
      expectedProfileRevision
    ) ||
    expectedProfileRevision < 0
  ) {
    throw new HttpError(
      400,
      'invalid_profile_revision'
    );
  }

  const ref =
    playerRef(accountHash);

  const opRef =
    ref
      .collection('operations')
      .doc(operationId);

  return db.runTransaction(
    async tx => {
      const [
        snap,
        opSnap
      ] = await Promise.all([
        tx.get(ref),
        tx.get(opRef)
      ]);

      const current =
        normalizePlayer(
          snap.exists
            ? snap.data()
            : defaultPlayer()
        );

      if (opSnap.exists) {
        const old =
          opSnap.data() || {};

        if (
          old.sku !== sku ||
          old.kind !== 'buy'
        ) {
          throw new HttpError(
            400,
            'operation_already_bound'
          );
        }

        return walletSnapshot(
          accountHash,
          current,
          operationId
        );
      }

      if (
        current.profileRevision !==
        expectedProfileRevision
      ) {
        throw new HttpError(
          409,
          'profile_conflict'
        );
      }

      const map =
        profileMap(
          current.profile
        );

      const owned =
        new Set(
          current.owned
        );

      const firstWrite =
        def.writes[0];

      const alreadyOwned =
        !def.consumable &&
        (
          owned.has(sku) ||
          (
            firstWrite &&
            map.get(
              firstWrite.key
            )?.kind === 0 &&
            map.get(
              firstWrite.key
            )?.number === 1
          )
        );

      let next = {
        ...current,
        owned: [...owned],
        profile:
          sortedProfileFromMap(
            map
          )
      };

      let contribution = 0;

      if (!alreadyOwned) {
        const shardEntry =
          map.get('DF_Shards');

        const earned =
          shardEntry?.kind === 0 &&
          Number.isInteger(
            shardEntry.number
          )
            ? Math.max(
                0,
                shardEntry.number
              )
            : 0;

        contribution =
          Math.min(
            earned,
            def.price
          );

        const paid =
          def.price -
          contribution;

        if (
          current.balance <
          paid
        ) {
          throw new HttpError(
            400,
            'insufficient_shards'
          );
        }

        map.set(
          'DF_Shards',
          {
            key: 'DF_Shards',
            kind: 0,
            number:
              earned -
              contribution
          }
        );

        if (sku === 'shield') {
          const old =
            map.get(
              'DF_BattleItem_2'
            );

          const count =
            old?.kind === 0 &&
            Number.isInteger(
              old.number
            )
              ? old.number
              : 0;

          if (
            count >=
            MAX_BALANCE
          ) {
            throw new HttpError(
              400,
              'inventory_capacity_exceeded'
            );
          }

          map.set(
            'DF_BattleItem_2',
            {
              key:
                'DF_BattleItem_2',
              kind: 0,
              number:
                count + 1
            }
          );
        } else {
          for (
            const write of
            def.writes
          ) {
            map.set(
              write.key,
              { ...write }
            );
          }

          owned.add(sku);
        }

        next = {
          ...current,

          balance:
            current.balance -
            paid,

          revision:
            current.revision +
            1,

          profileRevision:
            current.profileRevision +
            1,

          profile:
            sortedProfileFromMap(
              map
            ),

          owned:
            [...owned].sort()
        };
      } else {
        let restored = false;

        for (
          const write of
          def.writes
        ) {
          if (
            write.key.includes(
              'Owned'
            ) ||
            write.key ===
              'DF_BP_BETA01_Premium'
          ) {
            if (
              !sameEntry(
                map.get(
                  write.key
                ),
                write
              )
            ) {
              map.set(
                write.key,
                { ...write }
              );

              restored = true;
            }
          }
        }

        next = {
          ...current,

          profile:
            sortedProfileFromMap(
              map
            ),

          profileRevision:
            current.profileRevision +
            (restored ? 1 : 0),

          owned:
            [...owned].sort()
        };
      }

      tx.set(
        ref,
        {
          ...next,
          updatedAt:
            FieldValue
              .serverTimestamp()
        },
        { merge: true }
      );

      tx.create(
        opRef,
        {
          sku,
          localContribution:
            contribution,
          kind: 'buy',

          createdAt:
            FieldValue
              .serverTimestamp()
        }
      );

      return walletSnapshot(
        accountHash,
        next,
        operationId
      );
    }
  );
}

async function getGoogleClient() {
  if (!googleClientPromise) {
    googleClientPromise =
      googleAuth.getClient();
  }

  return googleClientPromise;
}

async function getGooglePurchase(
  purchaseToken
) {
  const client =
    await getGoogleClient();

  const url =
    'https://androidpublisher.googleapis.com/' +
    'androidpublisher/v3/' +
    `applications/${encodeURIComponent(PACKAGE_NAME)}/` +
    'purchases/productsv2/tokens/' +
    encodeURIComponent(
      purchaseToken
    );

  try {
    const response =
      await client.request({
        url,
        method: 'GET'
      });

    return response.data;
  } catch (error) {
    const status =
      Number(
        error?.response?.status ||
        0
      );

    if (
      status === 400 ||
      status === 404
    ) {
      throw new HttpError(
        400,
        'invalid_purchase_token'
      );
    }

    if (
      status === 401 ||
      status === 403
    ) {
      throw new HttpError(
        503,
        'google_play_api_not_authorized'
      );
    }

    throw new HttpError(
      503,
      'google_play_unavailable'
    );
  }
}

function subscriptionsConfigured() {
  return Boolean(
    process.env.DF_ENABLE_SHARD_SUBSCRIPTIONS === '1' &&
    process.env.DF_RTDN_AUDIENCE?.startsWith('https://') &&
    process.env.DF_RTDN_SERVICE_ACCOUNT?.endsWith('.gserviceaccount.com')
  );
}

function hashId(value) {
  return sha256(String(value));
}

function subscriptionRefs(accountHash, purchaseToken, orderId) {
  const tokenHash = hashId(purchaseToken);
  return {
    tokenHash,
    tokenRef: db.collection('subscriptionTokens').doc(tokenHash),
    stateRef: db.collection('subscriptions').doc(accountHash),
    orderRef: db.collection('subscriptionOrders').doc(hashId(orderId))
  };
}

async function verifyGoogleSubscription(purchaseToken) {
  const client = await getGoogleClient();
  const url =
    'https://androidpublisher.googleapis.com/androidpublisher/v3/' +
    `applications/${encodeURIComponent(PACKAGE_NAME)}/` +
    'purchases/subscriptionsv2/tokens/' +
    encodeURIComponent(purchaseToken);
  try {
    return (await client.request({ url, method: 'GET' })).data;
  } catch (error) {
    const status = Number(error?.response?.status || 0);
    if (status === 400 || status === 404) {
      throw new HttpError(400, 'invalid_purchase_token');
    }
    if (status === 401 || status === 403) {
      throw new HttpError(503, 'google_play_api_not_authorized');
    }
    throw new HttpError(503, 'google_play_unavailable');
  }
}

function validateSubscription(productId, verified, accountHash) {
  if (productId !== SUBSCRIPTION_ID || !verified || typeof verified !== 'object') {
    throw new HttpError(400, 'unknown_product');
  }
  const owner = verified.externalAccountIdentifiers?.obfuscatedExternalAccountId;
  if (!/^[a-f0-9]{64}$/.test(String(owner || '')) || owner !== accountHash) {
    throw new HttpError(403, 'purchase_account_mismatch');
  }
  const items = verified.lineItems;
  if (!Array.isArray(items) || items.length !== 1) {
    throw new HttpError(400, 'unsupported_subscription_items');
  }
  const line = items[0];
  if (
    line?.productId !== SUBSCRIPTION_ID ||
    line?.offerDetails?.basePlanId !== SUBSCRIPTION_BASE_PLAN ||
    Boolean(line?.offerDetails?.offerId) ||
    !line?.autoRenewingPlan
  ) {
    throw new HttpError(400, 'unsupported_subscription_plan');
  }
  const state = verified.subscriptionState;
  const allowed = new Set([
    ...SUBSCRIPTION_ACTIVE_STATES,
    'SUBSCRIPTION_STATE_PENDING',
    'SUBSCRIPTION_STATE_PAUSED',
    'SUBSCRIPTION_STATE_ON_HOLD',
    'SUBSCRIPTION_STATE_EXPIRED',
    'SUBSCRIPTION_STATE_PENDING_PURCHASE_CANCELED'
  ]);
  if (!allowed.has(state)) throw new HttpError(400, 'unknown_subscription_state');
  const expiry = Date.parse(line.expiryTime || '');
  if (!Number.isFinite(expiry) && !['SUBSCRIPTION_STATE_PENDING', 'SUBSCRIPTION_STATE_PENDING_PURCHASE_CANCELED'].includes(state)) {
    throw new HttpError(400, 'invalid_subscription_expiry');
  }
  const expiresAt = Number.isFinite(expiry) ? expiry : 0;
  const active = SUBSCRIPTION_ACTIVE_STATES.has(state) && expiresAt > Date.now();
  const latestOrderId = line.latestSuccessfulOrderId || null;
  if (active && (typeof latestOrderId !== 'string' || !latestOrderId.trim() || latestOrderId.length > 256)) {
    throw new HttpError(409, 'successful_paid_order_required');
  }
  return {
    accountHash, productId, state, expiresAt, active,
    renewing: line.autoRenewingPlan.autoRenewEnabled === true,
    latestOrderId
  };
}

async function getGoogleOrder(orderId) {
  const client = await getGoogleClient();
  const url =
    'https://androidpublisher.googleapis.com/androidpublisher/v3/' +
    `applications/${encodeURIComponent(PACKAGE_NAME)}/orders/` +
    encodeURIComponent(orderId);
  try {
    return (await client.request({ url, method: 'GET' })).data;
  } catch (error) {
    const status = Number(error?.response?.status || 0);
    if (status === 404) return null;
    if (status === 401 || status === 403) throw new HttpError(503, 'google_play_api_not_authorized');
    throw new HttpError(503, 'google_play_unavailable');
  }
}

function paidOrder(order, orderId, token, accountHash) {
  if (!order) return false;
  const lines = order.lineItems;
  if (
    order.orderId !== orderId || order.purchaseToken !== token ||
    !Array.isArray(lines) || lines.length !== 1
  ) throw new HttpError(409, 'subscription_order_binding_mismatch');
  const line = lines[0];
  const details = line.subscriptionDetails || {};
  if (
    line.productId !== SUBSCRIPTION_ID || details.basePlanId !== SUBSCRIPTION_BASE_PLAN ||
    details.offerId || !(details.offerPhaseDetails?.baseDetails || details.offerPhase === 'BASE')
  ) throw new HttpError(409, 'unsupported_paid_order_plan');
  if (order.state === 'PENDING' || order.state === 'PENDING_REFUND') {
    throw new HttpError(503, 'subscription_order_pending');
  }
  if (!['PROCESSED', 'CANCELED', 'REFUNDED', 'PARTIALLY_REFUNDED'].includes(order.state)) {
    throw new HttpError(503, 'subscription_order_unsettled');
  }
  if (order.state !== 'PROCESSED') return false;
  const total = line.total || order.total;
  if (!total || !/^[A-Z]{3}$/.test(total.currencyCode || '')) throw new HttpError(503, 'subscription_order_amount_unavailable');
  const units = Number(total.units || 0);
  const nanos = Number(total.nanos || 0);
  return Number.isFinite(units) && Number.isFinite(nanos) && units * 1e9 + nanos > 0;
}

async function knownSubscriptionOrder(accountHash, token, orderId) {
  const { tokenHash, orderRef } = subscriptionRefs(accountHash, token, orderId);
  const [tokenSnap, orderSnap] = await Promise.all([tokenRefFor(tokenHash).get(), orderRef.get()]);
  if (tokenSnap.exists) {
    const d = tokenSnap.data() || {};
    if (d.accountHash !== accountHash || d.productId !== SUBSCRIPTION_ID) {
      throw new HttpError(400, 'purchase_token_already_bound');
    }
  }
  if (orderSnap.exists) {
    const d = orderSnap.data() || {};
    if (d.accountHash !== accountHash || d.productId !== SUBSCRIPTION_ID || d.tokenHash !== tokenHash) {
      throw new HttpError(400, 'subscription_order_already_bound');
    }
    return true;
  }
  return false;
}

function tokenRefFor(tokenHash) {
  return db.collection('subscriptionTokens').doc(tokenHash);
}

async function grantSubscriptionOrder(accountHash, token, orderId) {
  const { tokenHash, tokenRef, orderRef } = subscriptionRefs(accountHash, token, orderId);
  if (await knownSubscriptionOrder(accountHash, token, orderId)) return;
  const player = playerRef(accountHash);
  await db.runTransaction(async tx => {
    const [playerSnap, tokenSnap, orderSnap] = await Promise.all([
      tx.get(player), tx.get(tokenRef), tx.get(orderRef)
    ]);
    const tokenData = tokenSnap.exists ? tokenSnap.data() || {} : {};
    const orderData = orderSnap.exists ? orderSnap.data() || {} : {};
    if (tokenSnap.exists && (tokenData.accountHash !== accountHash || tokenData.productId !== SUBSCRIPTION_ID)) {
      throw new HttpError(400, 'purchase_token_already_bound');
    }
    if (orderSnap.exists) {
      if (orderData.accountHash !== accountHash || orderData.productId !== SUBSCRIPTION_ID || orderData.tokenHash !== tokenHash) {
        throw new HttpError(400, 'subscription_order_already_bound');
      }
      return;
    }
    const current = normalizePlayer(playerSnap.exists ? playerSnap.data() : defaultPlayer());
    if (current.balance + SUBSCRIPTION_SHARDS > MAX_BALANCE) throw new HttpError(400, 'wallet_capacity_exceeded');
    const next = { ...current, balance: current.balance + SUBSCRIPTION_SHARDS, revision: current.revision + 1 };
    tx.set(player, { ...next, updatedAt: FieldValue.serverTimestamp() }, { merge: true });
    tx.create(orderRef, {
      accountHash, productId: SUBSCRIPTION_ID, tokenHash,
      orderId, credited: SUBSCRIPTION_SHARDS, createdAt: FieldValue.serverTimestamp()
    });
    tx.set(tokenRef, {
      accountHash, productId: SUBSCRIPTION_ID,
      updatedAt: FieldValue.serverTimestamp()
    }, { merge: true });
  });
}

async function refreshSubscriptionState(accountHash, token, value) {
  const { tokenHash, tokenRef, stateRef } = subscriptionRefs(accountHash, token, 'state');
  await db.runTransaction(async tx => {
    const tokenSnap = await tx.get(tokenRef);
    if (tokenSnap.exists) {
      const d = tokenSnap.data() || {};
      if (d.accountHash !== accountHash || d.productId !== SUBSCRIPTION_ID) {
        throw new HttpError(400, 'purchase_token_already_bound');
      }
    }
    tx.set(tokenRef, {
      accountHash, productId: SUBSCRIPTION_ID,
      updatedAt: FieldValue.serverTimestamp()
    }, { merge: true });
    tx.set(stateRef, {
      tokenHash, productId: SUBSCRIPTION_ID,
      state: value.state, expiresAt: value.expiresAt,
      renewing: value.renewing,
      updatedAt: FieldValue.serverTimestamp()
    }, { merge: true });
  });
}

async function syncSubscription(productId, purchaseToken, accountHash) {
  if (!subscriptionsConfigured()) throw new HttpError(503, 'subscriptions_unavailable');
  if (productId !== SUBSCRIPTION_ID) throw new HttpError(400, 'unknown_product');
  if (typeof purchaseToken !== 'string' || !purchaseToken.trim() || purchaseToken.length > 8192) {
    throw new HttpError(400, 'invalid_purchase_token');
  }
  const verified = await verifyGoogleSubscription(purchaseToken);
  const value = validateSubscription(productId, verified, accountHash);
  if (value.latestOrderId) {
    const match = value.latestOrderId.match(/^(GPA\.[A-Za-z0-9.-]+?)\.\.(\d+)$/);
    const renewalIndex = match ? Number(match[2]) : 0;
    if (match && (!Number.isSafeInteger(renewalIndex) || renewalIndex > 1000)) {
      throw new HttpError(503, 'subscription_history_reconciliation_required');
    }
    const candidates = match
      ? [match[1], ...Array.from({ length: renewalIndex + 1 }, (_, i) => `${match[1]}..${i}`)]
      : [value.latestOrderId];
    const missing = [];
    for (const orderId of candidates) {
      if (!(await knownSubscriptionOrder(accountHash, purchaseToken, orderId))) missing.push(orderId);
    }
    for (const orderId of missing.slice(0, 4)) {
      const order = await getGoogleOrder(orderId);
      if (!order && orderId !== value.latestOrderId) continue;
      if (!order) throw new HttpError(503, 'subscription_order_unavailable');
      if (paidOrder(order, orderId, purchaseToken, accountHash)) {
        await grantSubscriptionOrder(accountHash, purchaseToken, orderId);
      } else if (order.state === 'PENDING') {
        throw new HttpError(503, 'subscription_order_pending');
      } else {
        // Persist non-paid order binding so old canceled/refunded IDs are not retried forever.
        const { tokenHash, tokenRef, orderRef } = subscriptionRefs(accountHash, purchaseToken, orderId);
        await db.runTransaction(async tx => {
          const [t, o] = await Promise.all([tx.get(tokenRef), tx.get(orderRef)]);
          if (t.exists && t.data()?.accountHash !== accountHash) throw new HttpError(400, 'purchase_token_already_bound');
          if (o.exists && (o.data()?.accountHash !== accountHash || o.data()?.tokenHash !== tokenHash)) throw new HttpError(400, 'subscription_order_already_bound');
          if (!o.exists) tx.create(orderRef, { accountHash, productId: SUBSCRIPTION_ID, tokenHash, orderId, credited: 0, createdAt: FieldValue.serverTimestamp() });
          tx.set(tokenRef, { accountHash, productId: SUBSCRIPTION_ID, updatedAt: FieldValue.serverTimestamp() }, { merge: true });
        });
      }
    }
    if (missing.length > 4) throw new HttpError(503, 'subscription_history_reconciliation_required');
  }
  await refreshSubscriptionState(accountHash, purchaseToken, value);
  if (value.active && verified.acknowledgementState === 'ACKNOWLEDGEMENT_STATE_PENDING') {
    const client = await getGoogleClient();
    const url =
      'https://androidpublisher.googleapis.com/androidpublisher/v3/' +
      `applications/${encodeURIComponent(PACKAGE_NAME)}/purchases/subscriptions/` +
      `${encodeURIComponent(SUBSCRIPTION_ID)}/tokens/${encodeURIComponent(purchaseToken)}:acknowledge`;
    try { await client.request({ url, method: 'POST', data: {} }); }
    catch { throw new HttpError(503, 'google_play_acknowledgement_pending'); }
  }
  return readWallet(accountHash);
}

async function verifyGooglePush(req) {
  const audience = process.env.DF_RTDN_AUDIENCE || '';
  const serviceAccount = process.env.DF_RTDN_SERVICE_ACCOUNT || '';
  const header = String(req.get('authorization') || '');
  if (!subscriptionsConfigured() || !header.startsWith('Bearer ')) throw new HttpError(401, 'push_authentication_required');
  try {
    const { payload } = await jwtVerify(header.slice(7), GOOGLE_JWKS, {
      algorithms: ['RS256'], issuer: ['accounts.google.com', 'https://accounts.google.com'], audience
    });
    if (payload.email !== serviceAccount || payload.email_verified !== true) throw new Error('identity mismatch');
  } catch { throw new HttpError(401, 'invalid_push_authentication'); }
}

async function handleSubscriptionPush(req) {
  await verifyGooglePush(req);
  const envelope = req.body && typeof req.body === 'object' ? req.body : {};
  let payload;
  try {
    const encoded = envelope.message?.data;
    if (typeof encoded !== 'string' || encoded.length > 65536) throw new Error('invalid data');
    payload = JSON.parse(Buffer.from(encoded, 'base64').toString('utf8'));
  } catch { throw new HttpError(400, 'invalid_notification'); }
  if (!payload || typeof payload !== 'object' || payload.packageName !== PACKAGE_NAME) throw new HttpError(400, 'notification_package_mismatch');
  if (payload.testNotification) return;
  const notice = payload.subscriptionNotification;
  if (!notice) return;
  if (notice.subscriptionId !== SUBSCRIPTION_ID) return;
  const verified = await verifyGoogleSubscription(String(notice.purchaseToken || ''));
  const owner = verified.externalAccountIdentifiers?.obfuscatedExternalAccountId;
  if (!/^[a-f0-9]{64}$/.test(String(owner || ''))) throw new HttpError(403, 'purchase_account_mismatch');
  await syncSubscription(SUBSCRIPTION_ID, String(notice.purchaseToken), owner);
}

async function fulfillGooglePurchase(
  accountHash,
  productId,
  purchaseToken
) {
  const shardsPerUnit =
    PRODUCT_SHARDS[
      productId
    ];

  if (
    !Number.isInteger(
      shardsPerUnit
    ) ||
    shardsPerUnit <= 0
  ) {
    throw new HttpError(
      400,
      'unknown_product'
    );
  }

  if (
    typeof purchaseToken !==
      'string' ||
    !purchaseToken.trim() ||
    purchaseToken.length > 8192
  ) {
    throw new HttpError(
      400,
      'invalid_purchase_token'
    );
  }

  const tokenHash =
    sha256(purchaseToken);

  const purchaseRef =
    db.collection('purchases')
      .doc(tokenHash);

  const existing =
    await purchaseRef.get();

  if (existing.exists) {
    const old =
      existing.data() || {};

    if (
      old.accountHash !==
        accountHash ||
      old.productId !==
        productId
    ) {
      throw new HttpError(
        400,
        'purchase_token_already_bound'
      );
    }

    return readWallet(
      accountHash
    );
  }

  const purchase =
    await getGooglePurchase(
      purchaseToken
    );

  if (
    purchase
      ?.purchaseStateContext
      ?.purchaseState !==
    'PURCHASED'
  ) {
    throw new HttpError(
      409,
      'purchase_not_completed'
    );
  }

  if (
    purchase
      ?.obfuscatedExternalAccountId !==
    accountHash
  ) {
    throw new HttpError(
      403,
      'purchase_account_mismatch'
    );
  }

  const lineItems =
    Array.isArray(
      purchase
        ?.productLineItem
    )
      ? purchase
          .productLineItem
      : [];

  const lineItem =
    lineItems.find(
      x =>
        x?.productId ===
        productId
    );

  if (!lineItem) {
    throw new HttpError(
      400,
      'product_mismatch'
    );
  }

  const quantity =
    Number(
      lineItem
        ?.productOfferDetails
        ?.quantity ?? 1
    );

  if (
    !Number.isInteger(
      quantity
    ) ||
    quantity < 1 ||
    quantity > 100
  ) {
    throw new HttpError(
      400,
      'invalid_quantity'
    );
  }

  const refundableQuantity =
    Number(
      lineItem
        ?.productOfferDetails
        ?.refundableQuantity ??
      quantity
    );

  if (
    !Number.isInteger(
      refundableQuantity
    ) ||
    refundableQuantity !==
      quantity
  ) {
    throw new HttpError(
      409,
      'purchase_refunded_or_partially_refunded'
    );
  }

  if (
    lineItem
      ?.productOfferDetails
      ?.consumptionState ===
    'CONSUMPTION_STATE_CONSUMED'
  ) {
    throw new HttpError(
      409,
      'purchase_already_consumed'
    );
  }

  const amount =
    shardsPerUnit *
    quantity;

  const ref =
    playerRef(
      accountHash
    );

  return db.runTransaction(
    async tx => {
      const [
        purchaseSnap,
        playerSnap
      ] = await Promise.all([
        tx.get(purchaseRef),
        tx.get(ref)
      ]);

      const current =
        normalizePlayer(
          playerSnap.exists
            ? playerSnap.data()
            : defaultPlayer()
        );

      if (
        purchaseSnap.exists
      ) {
        const old =
          purchaseSnap.data() ||
          {};

        if (
          old.accountHash !==
            accountHash ||
          old.productId !==
            productId ||
          old.quantity !==
            quantity
        ) {
          throw new HttpError(
            400,
            'purchase_token_already_bound'
          );
        }

        return walletSnapshot(
          accountHash,
          current
        );
      }

      if (
        current.balance +
          amount >
        MAX_BALANCE
      ) {
        throw new HttpError(
          400,
          'wallet_capacity_exceeded'
        );
      }

      const next = {
        ...current,

        balance:
          current.balance +
          amount,

        revision:
          current.revision +
          1
      };

      tx.set(
        ref,
        {
          ...next,

          updatedAt:
            FieldValue
              .serverTimestamp()
        },
        { merge: true }
      );

      tx.create(
        purchaseRef,
        {
          accountHash,
          productId,
          quantity,
          credited: amount,

          orderId:
            purchase?.orderId ||
            null,

          regionCode:
            purchase?.regionCode ||
            null,

          testPurchase:
            Boolean(
              purchase
                ?.testPurchaseContext
            ),

          createdAt:
            FieldValue
              .serverTimestamp()
        }
      );

      return walletSnapshot(
        accountHash,
        next
      );
    }
  );
}

async function success(
  res,
  wallet
) {
  const accountHash = wallet?.accountHash;
  wallet.billingReady = true;
  wallet.subscriptionsReady = subscriptionsConfigured();
  wallet.subscriptionActive = false;

  if (accountHash && wallet.subscriptionsReady) {
    try {
      const stateSnap = await db.collection('subscriptions').doc(accountHash).get();
      const state = stateSnap.exists ? stateSnap.data() || {} : {};
      wallet.subscriptionActive = Boolean(
        SUBSCRIPTION_ACTIVE_STATES.has(state.state) &&
        Number(state.expiresAt) > Date.now()
      );
    } catch (error) {
      // An optional billing lookup must not hide an already loaded account.
      // Fail closed for purchases until a later request can read billing state.
      wallet.billingReady = false;
      wallet.subscriptionsReady = false;
      console.error('Subscription state unavailable; account synchronization preserved.', {
        code: Number.isInteger(error?.code) ? error.code : 'unavailable'
      });
    }
  }
  res.set(
    'Cache-Control',
    'no-store'
  );

  return res
    .status(200)
    .json({
      fulfilled: true,
      wallet
    });
}

function failure(
  res,
  status,
  code
) {
  const messages = {
    invalid_ranking_offset: 'Invalid ranking page. Open the first ranking page and retry.',
    ranking_directory_limit: 'Player ranking needs a server database index before this directory can grow further.',
    social_feature_not_deployed: 'This account server supports player levels. Clan features still need to be deployed.'
  };
  res.set(
    'Cache-Control',
    'no-store'
  );

  return res
    .status(status)
    .json({
      fulfilled: false,
      error: code,
      ...(messages[code] ? { message: messages[code] } : {})
    });
}

http(
  'helloHttp',
  async (req, res) => {
    try {
      if (
        req.method === 'GET' &&
        req.path === '/'
      ) {
        return res
          .status(200)
          .json({
            ok: true,
            revision: SERVER_REVISION,
            service:
              'dimensions-front-purchase-server'
          });
      }

      if (req.path === '/google-play/rtdn') {
        if (req.method !== 'POST') return failure(res, 405, 'method_not_allowed');
        await handleSubscriptionPush(req);
        res.set('Cache-Control', 'no-store');
        return res.status(204).send();
      }

      if (
        req.method !== 'POST'
      ) {
        return failure(
          res,
          405,
          'method_not_allowed'
        );
      }

      const {
        accountHash
      } =
        await authenticateUnity(
          req
        );

      const body =
        req.body &&
        typeof req.body ===
          'object'
          ? req.body
          : {};

      if (req.path === '/social/levels') {
        const social = await readPlayerLevels(accountHash, body);
        res.set('Cache-Control', 'no-store');
        return res.status(200).json({ fulfilled: true, social });
      }

      if (req.path.startsWith('/social/')) {
        throw new HttpError(503, 'social_feature_not_deployed');
      }

      if (
        req.path ===
        '/wallet'
      ) {
        return success(
          res,
          await readWallet(
            accountHash
          )
        );
      }

      if (
        req.path ===
        '/profile/save'
      ) {
        return success(
          res,
          await saveProfile(
            accountHash,
            body.profile,
            body.expectedProfileRevision
          )
        );
      }

      if (
        req.path ===
        '/wallet/spend'
      ) {
        const sku =
          String(
            body.cosmeticId ||
            ''
          ).trim();

        const operationId =
          String(
            body.operationId ||
            ''
          ).trim();

        return success(
          res,
          await spendWallet(
            accountHash,
            sku,
            operationId,
            body.expectedProfileRevision
          )
        );
      }

      if (
        req.path ===
        '/google-play/fulfill'
      ) {
        const productId =
          String(
            body.productId ||
            ''
          ).trim();

        const purchaseToken =
          String(
            body.purchaseToken ||
            ''
          ).trim();

        return success(
          res,
          await fulfillGooglePurchase(
            accountHash,
            productId,
            purchaseToken
          )
        );
      }

      if (req.path === '/google-play/subscription') {
        return success(
          res,
          await syncSubscription(
            String(body.productId || '').trim(),
            String(body.purchaseToken || '').trim(),
            accountHash
          )
        );
      }

      return failure(
        res,
        404,
        'unknown_route'
      );
    } catch (error) {
      const status =
        error instanceof
        HttpError
          ? error.status
          : 500;

      const code =
        error instanceof
        HttpError
          ? error.code
          : 'server_error';

      if (
        !(
          error instanceof
          HttpError
        )
      ) {
        console.error(
          'Purchase server error:',
          error?.name ||
            'unknown_error'
        );
      }

      return failure(
        res,
        status,
        code
      );
    }
  }
);
