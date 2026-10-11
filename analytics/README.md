# Private Firebase usage dashboard

`/admin/` provides opt-in installation counts, recent activity, launcher-version distribution and categorized errors. Sign in with the designated verified Google account. It does not report total people or all installations: only opted-in installation identities are represented. Reinstallation can create a new identity. Opting out attempts to delete the identity records; offline deletion can fail.

The page uses Firebase's official modular JavaScript SDK via its pinned CDN version. `admin-config.json` contains public web app configuration, not an administrator credential. Copy `site/admin/admin-config.example.json` to that ignored file and supply your Firebase web app values. Enable Google and Anonymous authentication and authorize the hosting domain in Firebase Authentication.

## Rules

`database.rules.json` remains the default deny-all policy for retired accounts. The analytics rules are an explicit deployment opt-in. Replace `ADMIN_EMAIL` in `analytics/database.rules.template.json` with the designated verified administrator email, write `analytics/database.rules.deployed.json` (ignored), and deploy using the private metrics config. Do not deploy the template with its placeholder.

Clients can only write their own anonymous Firebase UID records. The administrator can only read metrics, not write them. Each installation has a bounded version string and server timestamp fields. Each UID has ten allowlisted error categories with cumulative counts; raw exceptions, player names, emails and mod lists are rejected. Existing `activeSessions` and `launcherProfiles` remain inaccessible, including to the metrics administrator.

The panel's “last 24 hours errors” count is the number of per-installation categories whose latest report occurred in that interval. It is not the number of individual errors during those 24 hours. Each displayed category count is cumulative. A session is active if it reported within five minutes; this is a heartbeat estimate, not a guarantee that someone is playing.

Anonymous client reports can be forged by a modified client. These are operational estimates, not billing or anti-abuse metrics. Rules enforce identity ownership and structure, but do not establish one identity per human.

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

Official references: [Web SDK setup](https://firebase.google.com/docs/web/setup), [Google sign-in](https://firebase.google.com/docs/auth/web/google-signin), [Realtime Database rules](https://firebase.google.com/docs/database/security).

For redirect fallback on browsers that block popups, set the runtime `authDomain` to the Firebase Hosting domain serving this panel and register `https://<domain>/__/auth/handler` in Google OAuth authorized redirect URIs. Keep that domain in Authentication authorized domains. [Firebase redirect guidance](https://firebase.google.com/docs/auth/web/redirect-best-practices).
