# Private Firebase usage dashboard

`/admin/` provides opt-in installation counts, recent activity, launcher-version distribution and categorized errors. Sign in with the designated verified Google account. It does not report total people or all installations: only opted-in installation identities are represented. Reinstallation can create a new identity. Opting out attempts to delete the identity records; offline deletion can fail.

The page uses Firebase's official modular JavaScript SDK via its pinned CDN version. `admin-config.json` contains public web app configuration, not an administrator credential. Copy `site/admin/admin-config.example.json` to that ignored file and supply your Firebase web app values. Enable Google and Anonymous authentication and authorize the hosting domain in Firebase Authentication.

## Rules

`database.rules.json` remains the default deny-all policy for retired accounts. The analytics rules are an explicit deployment opt-in. Replace `ADMIN_EMAIL` in `analytics/database.rules.template.json` with the designated verified administrator email, write `analytics/database.rules.deployed.json` (ignored), and deploy using the private metrics config. Do not deploy the template with its placeholder.

Clients can only write their own anonymous Firebase UID records. The administrator can only read metrics, not write them. Each installation has a bounded version string and server timestamp fields. Each UID has ten allowlisted error categories with cumulative counts; raw exceptions, player names, emails and mod lists are rejected. Existing `activeSessions` and `launcherProfiles` remain inaccessible, including to the metrics administrator.

The panel's “last 24 hours errors” count is the number of per-installation categories whose latest report occurred in that interval. It is not the number of individual errors during those 24 hours. Each displayed category count is cumulative. A session is active if it reported within five minutes; this is a heartbeat estimate, not a guarantee that someone is playing.

Anonymous client reports can be forged by a modified client. These are operational estimates, not billing or anti-abuse metrics. Rules enforce identity ownership and structure, but do not establish one identity per human.

## Optional launcher support

`launcherSupport` is separate from normal anonymous usage metrics. Explicit support sharing exposes the device's chosen username, launcher version, selected profile and up to 300 mod basenames with enabled flags to the designated verified Google administrator. The anonymous installation may read, replace or delete only its own device record. Other installations and unauthenticated visitors cannot read it. `lastSeen` must be within five minutes of server time. Empty inventories may omit `mods`, because Realtime Database removes empty arrays. Indices are limited to 0–299; filenames cannot contain path separators or control characters. `modCount` and `truncated` are bounded client reports, not independently verified totals.

The administrator may create one pending `launcherSupport/requests/<uid>` record only while that device is recent (five minutes) and `allowRequests` is true. A request contains a random 32-character lowercase hexadecimal ID, an eight-character Modrinth project ID, display name, exact current profile and server timestamp. URLs, local paths, commands and extra fields are rejected. The administrator can read pending requests but cannot overwrite or delete them. The owner can read and delete its request, including after deleting its device record, but cannot create or change a request. A request is a pending support proposal; these database permissions do not authorize executing commands or installing files without the launcher's own validation and user consent.

The template adds these narrowly scoped rules without granting writes at the support root or changing the existing metrics and retired-account policies. Publish only a reviewed private rules file with the administrator placeholder replaced.

## Checks

Run frontend aggregation and configuration checks:

```
node --test Validation/FirebaseMetricsTests.js
```

For actual RTDB rule allow/deny tests, use Java 21 and temporary development dependencies (not launcher dependencies):

```
npm install --prefix artifacts/firebase-tests --no-audit --no-fund @firebase/rules-unit-testing firebase
npx firebase-tools@15.33.0 emulators:exec --only database --project demo-mistik-metrics --config analytics/firebase.emulator.json "node Validation/FirebaseMetricsEmulator.mjs"
```

The emulator suite uses a demo namespace and a synthetic administrator email. It exercises own-record writes/reads, stranger and guest denial, verified Google administrator reads, stale/future timestamps, immutable firstSeen, extra fields, unsupported versions/codes, atomic server increments, count bounds and opt-out deletion.

Run the separate support suite against the same isolated emulator configuration:

```
npx firebase-tools@15.33.0 emulators:exec --only database --project demo-mistik-metrics --config analytics/firebase.emulator.json "node Validation/LauncherSupportEmulator.mjs"
```

It covers device ownership, administrator/provider verification, consent and freshness cutoffs, bounded inventories, path/control-character rejection, strict request fields, profile matching, create-only requests and owner cleanup after device deletion. Run emulator suites sequentially: each clears its demo database.

Official references: [Web SDK setup](https://firebase.google.com/docs/web/setup), [Google sign-in](https://firebase.google.com/docs/auth/web/google-signin), [Realtime Database rules](https://firebase.google.com/docs/database/security).

For redirect login without popup windows, set the runtime `authDomain` to the Firebase Hosting domain serving this panel and register `https://<domain>/__/auth/handler` in Google OAuth authorized redirect URIs. Keep that domain in Authentication authorized domains. [Firebase redirect guidance](https://firebase.google.com/docs/auth/web/redirect-best-practices).
