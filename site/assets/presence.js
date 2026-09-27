
    const count = document.getElementById('active-count');
    const status = document.getElementById('active-status');
    const say = key => window.mistikWords?.[document.documentElement.lang === 'en' ? 'en' : 'tr']?.[`presence.${key}`] || '';
    let sessions = {}, serverOffset = 0, loaded = false;
    const render = () => {
      if (!loaded) return;
      const cutoff = Date.now() + serverOffset - 120_000;
      let total = 0;
      for (const userSessions of Object.values(sessions))
        for (const session of Object.values(userSessions || {}))
          if (Number.isFinite(Number(session?.lastSeen)) && Number(session.lastSeen) >= cutoff) total++;
      count.textContent = total.toLocaleString(document.documentElement.lang === 'en' ? 'en-US' : 'tr-TR');
      status.textContent = say('live');
    };
    const failed = () => { count.textContent = '—'; status.textContent = say('error'); };
    try {
      const [{ initializeApp }, { getDatabase, onValue, ref }] = await Promise.all([
        import('https://www.gstatic.com/firebasejs/12.19.0/firebase-app.js'),
        import('https://www.gstatic.com/firebasejs/12.19.0/firebase-database.js')
      ]);
      const app = initializeApp({
        apiKey: 'AIzaSyCP7R_Q8Ai_b_zKTCBRk9d1kkZY3iilENg',
        authDomain: 'mistiklauncher-9eb4b.firebaseapp.com',
        databaseURL: 'https://mistiklauncher-9eb4b-default-rtdb.firebaseio.com',
        projectId: 'mistiklauncher-9eb4b',
        appId: '1:369825747453:web:0db84ff80d70903e444994',
        messagingSenderId: '369825747453'
      });
      const db = getDatabase(app);
      onValue(ref(db, '.info/serverTimeOffset'), snap => { serverOffset = Number(snap.val()) || 0; render(); }, failed);
      onValue(ref(db, 'activeSessions'), snap => { sessions = snap.val() || {}; loaded = true; render(); }, failed);
      setInterval(render, 15_000);
      new MutationObserver(render).observe(document.documentElement, { attributes: true, attributeFilter: ['lang'] });
    } catch { failed(); }
