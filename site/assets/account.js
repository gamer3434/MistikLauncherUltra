import { initializeApp } from 'https://www.gstatic.com/firebasejs/12.19.0/firebase-app.js';
import {
  createUserWithEmailAndPassword,
  getAuth,
  onAuthStateChanged,
  reload,
  sendEmailVerification,
  sendPasswordResetEmail,
  signInWithEmailAndPassword,
  signOut
} from 'https://www.gstatic.com/firebasejs/12.19.0/firebase-auth.js';

const firebaseConfig = {
  apiKey: 'AIzaSyCP7R_Q8Ai_b_zKTCBRk9d1kkZY3iilENg',
  authDomain: 'mistiklauncher-9eb4b.firebaseapp.com',
  projectId: 'mistiklauncher-9eb4b',
  appId: '1:369825747453:web:0db84ff80d70903e444994'
};

const words = {
  tr: {
    skip: 'İçeriğe geç', navFeatures: 'Özellikler', navPreview: 'Önizleme', navDownload: 'İndir', navAccount: 'Hesap', eyebrow: 'MİSTIK LAUNCHER HESABI', storyTitle: 'Bir hesap. İki yerde.',
    storyCopy: 'Sitede kayıt ol; aynı hesapla Mıstık Launcher\'da oturum aç. Firebase e-posta adresini yönetir, şifren bu sayfada saklanmaz.',
    storyNote: 'Ayarlarını launcher üzerinden istediğinde yedekle.', formKicker: 'HESAP ERİŞİMİ', loginTitle: 'Tekrar hoş geldin.', loginIntro: 'Hesabına giriş yapmak için e-posta adresini ve şifreni kullan.',
    signupTitle: 'Hesabını oluştur.', signupIntro: 'Mistik Launcher hesabın için e-posta adresini ve bir şifre belirle.', resetTitle: 'Şifreni yenile.', resetIntro: 'Hesabına bağlı e-posta adresine sıfırlama bağlantısı gönderelim.',
    loginTab: 'Giriş yap', signupTab: 'Hesap oluştur', emailLabel: 'E-posta adresi', emailPlaceholder: 'ornek@mail.com', passwordLabel: 'Şifre', forgot: 'Şifremi unuttum',
    passwordPlaceholder: 'En az 6 karakter', confirmLabel: 'Şifreyi tekrar gir', confirmPlaceholder: 'Şifreni tekrar yaz', loginSubmit: 'Giriş yap', signupSubmit: 'Hesap oluştur',
    resetEmailLabel: 'Hesabındaki e-posta adresi', resetSubmit: 'Sıfırlama bağlantısı gönder', resetBack: 'Girişe dön', signedInLabel: 'OTURUM AÇIK',
    signedInCopy: 'Hesabına giriş yaptın.', signOut: 'Çıkış yap', securityNote: 'Şifren kaydedilmez. Hesabın güvenli biçimde Firebase Auth ile yönetilir.',
    verificationPending: 'E-posta adresini doğrulaman gerekiyor. Gelen kutunu kontrol et.', verificationDone: 'E-posta adresin doğrulandı.',
    verificationSent: 'Doğrulama bağlantısını gönderdik. Gelen kutunu ve spam klasörünü kontrol et.', verificationError: 'Doğrulama e-postası gönderilemedi. Biraz sonra tekrar dene.',
    verificationRefreshError: 'Doğrulama durumu alınamadı. Biraz sonra tekrar dene.', resendVerification: 'Doğrulama e-postasını yeniden gönder', refreshVerification: 'Doğruladım, durumu yenile',
    passwordMismatch: 'Şifreler eşleşmiyor. İki alanı da kontrol et.',
    'auth/email-already-in-use': 'Hesap oluşturulamadı. Bilgileri kontrol edip tekrar dene.', 'auth/invalid-email': 'Geçerli bir e-posta adresi gir.',
    'auth/weak-password': 'Daha güçlü bir şifre seç. En az 6 karakter kullan.', 'auth/password-does-not-meet-requirements': 'Şifre Firebase hesabının güvenlik koşullarını karşılamıyor.',
    'auth/invalid-credential': 'E-posta veya şifre doğru değil. Bilgilerini kontrol edip tekrar dene.', 'auth/user-not-found': 'E-posta veya şifre doğru değil. Bilgilerini kontrol edip tekrar dene.',
    'auth/wrong-password': 'E-posta veya şifre doğru değil. Bilgilerini kontrol edip tekrar dene.', 'auth/too-many-requests': 'Çok fazla deneme yapıldı. Biraz bekleyip tekrar dene.',
    'auth/network-request-failed': 'Bağlantı kurulamadı. İnternetini kontrol edip tekrar dene.', 'auth/operation-not-allowed': 'E-posta ve şifreyle giriş şu anda etkin değil. Site yöneticisiyle iletişime geç.',
    'auth/unauthorized-domain': 'Bu site adresi Firebase Auth için yetkilendirilmemiş.', 'auth/invalid-api-key': 'Hesap hizmeti yapılandırması geçersiz.',
    'auth/configuration-not-found': 'Hesap hizmeti henüz yapılandırılmamış.', 'auth/internal-error': 'İşlem tamamlanamadı. Biraz sonra tekrar dene.',
    errorFallback: 'İşlem tamamlanamadı. Bilgilerini kontrol edip tekrar dene.', loginSuccess: 'Giriş başarılı.', signupSuccess: 'Hesabın oluşturuldu.',
    resetSuccess: 'Bu e-posta bir hesaba bağlıysa sıfırlama bağlantısı gönderildi. Gelen kutunu kontrol et.', signOutSuccess: 'Çıkış yaptın.'
  },
  en: {
    skip: 'Skip to content', navFeatures: 'Features', navPreview: 'Preview', navDownload: 'Download', navAccount: 'Account', eyebrow: 'MISTIK LAUNCHER ACCOUNT', storyTitle: 'One account. Both places.',
    storyCopy: 'Register here, then use the same account to sign in to Mistik Launcher. Firebase Auth manages your email; this page never stores your password.',
    storyNote: 'Back up your settings from the launcher whenever you choose.', formKicker: 'ACCOUNT ACCESS', loginTitle: 'Welcome back.', loginIntro: 'Use your email and password to sign in to your account.',
    signupTitle: 'Create your account.', signupIntro: 'Choose an email address and password for your Mistik Launcher account.', resetTitle: 'Reset your password.', resetIntro: 'We’ll send a reset link to the email address on your account.',
    loginTab: 'Sign in', signupTab: 'Create account', emailLabel: 'Email address', emailPlaceholder: 'name@example.com', passwordLabel: 'Password', forgot: 'Forgot password?',
    passwordPlaceholder: 'At least 6 characters', confirmLabel: 'Confirm password', confirmPlaceholder: 'Enter your password again', loginSubmit: 'Sign in', signupSubmit: 'Create account',
    resetEmailLabel: 'Email address on your account', resetSubmit: 'Send reset link', resetBack: 'Back to sign in', signedInLabel: 'SIGNED IN',
    signedInCopy: 'You are signed in to your account.', signOut: 'Sign out', securityNote: 'Your password is never stored. Firebase Auth manages your account securely.',
    verificationPending: 'Please verify your email address. Check your inbox.', verificationDone: 'Your email address is verified.',
    verificationSent: 'We sent a verification link. Check your inbox and spam folder.', verificationError: 'Could not send the verification email. Try again in a moment.',
    verificationRefreshError: 'Could not refresh verification status. Try again in a moment.', resendVerification: 'Resend verification email', refreshVerification: 'I verified it; refresh status',
    passwordMismatch: 'Passwords do not match. Check both fields.',
    'auth/email-already-in-use': 'Could not create the account. Check your details and try again.', 'auth/invalid-email': 'Enter a valid email address.',
    'auth/weak-password': 'Choose a stronger password with at least 6 characters.', 'auth/password-does-not-meet-requirements': 'Your password does not meet this Firebase account’s security requirements.',
    'auth/invalid-credential': 'Email or password is incorrect. Check your details and try again.', 'auth/user-not-found': 'Email or password is incorrect. Check your details and try again.',
    'auth/wrong-password': 'Email or password is incorrect. Check your details and try again.', 'auth/too-many-requests': 'Too many attempts. Wait a little and try again.',
    'auth/network-request-failed': 'Could not connect. Check your internet connection and try again.', 'auth/operation-not-allowed': 'Email and password sign-in is not enabled. Contact the site administrator.',
    'auth/unauthorized-domain': 'This site address is not authorized for Firebase Auth.', 'auth/invalid-api-key': 'The account service configuration is invalid.',
    'auth/configuration-not-found': 'The account service has not been configured yet.', 'auth/internal-error': 'The request could not be completed. Try again in a moment.',
    errorFallback: 'The request could not be completed. Check your details and try again.', loginSuccess: 'Signed in successfully.', signupSuccess: 'Your account has been created.',
    resetSuccess: 'If this email belongs to an account, a reset link has been sent. Check your inbox.', signOutSuccess: 'You have signed out.'
  }
};

let language = document.documentElement.lang === 'en' ? 'en' : 'tr';
const app = initializeApp(firebaseConfig);
const auth = getAuth(app);
auth.languageCode = language;
const languageButton = document.getElementById('language');
const accountTitle = document.getElementById('account-title');
const accountIntro = document.getElementById('account-intro');
const authForm = document.getElementById('auth-form');
const resetForm = document.getElementById('reset-form');
const confirmWrap = document.getElementById('confirm-wrap');
const passwordInput = document.getElementById('password');
const confirmInput = document.getElementById('password-confirm');
const submitButton = document.getElementById('auth-submit');
const status = document.getElementById('account-status');
const signedIn = document.getElementById('signed-in');
const views = document.getElementById('auth-views');
let mode = 'login';
let showingReset = false;
let currentUser = null;

function message(key) {
  return words[language][key] || words[language].errorFallback;
}

function setStatus(key, kind = '') {
  status.textContent = message(key);
  status.className = `account-status${kind ? ` is-${kind}` : ''}`;
}

function clearStatus() {
  status.textContent = '';
  status.className = 'account-status';
}

function renderVerification(user, state = '') {
  const verificationStatus = document.getElementById('verification-status');
  const verified = user.emailVerified;
  verificationStatus.textContent = message(verified ? 'verificationDone' : state || 'verificationPending');
  verificationStatus.className = `verification-status${state ? ` is-${state === 'verificationError' ? 'error' : 'success'}` : ''}`;
  document.getElementById('verification-actions').hidden = verified;
}

function setMode(nextMode) {
  mode = nextMode;
  showingReset = false;
  authForm.hidden = false;
  resetForm.hidden = true;
  confirmWrap.hidden = mode !== 'signup';
  confirmInput.required = mode === 'signup';
  passwordInput.autocomplete = mode === 'signup' ? 'new-password' : 'current-password';
  accountTitle.textContent = message(mode === 'signup' ? 'signupTitle' : 'loginTitle');
  accountIntro.textContent = message(mode === 'signup' ? 'signupIntro' : 'loginIntro');
  submitButton.textContent = message(mode === 'signup' ? 'signupSubmit' : 'loginSubmit');
  document.querySelectorAll('[data-mode]').forEach(button => {
    const active = button.dataset.mode === mode;
    button.setAttribute('aria-pressed', String(active));
    button.classList.toggle('is-active', active);
  });
  clearStatus();
}

function translatePage() {
  const dictionary = words[language];
  document.documentElement.lang = language;
  document.title = document.body.dataset[language === 'tr' ? 'titleTr' : 'titleEn'];
  languageButton.textContent = language === 'tr' ? 'EN' : 'TR';
  languageButton.setAttribute('aria-label', language === 'tr' ? 'Switch language to English' : 'Change language to Turkish');
  document.querySelectorAll('[data-i18n]').forEach(element => {
    const text = dictionary[element.dataset.i18n];
    if (text) element.textContent = text;
  });
  document.querySelectorAll('[data-i18n-placeholder]').forEach(element => {
    element.placeholder = dictionary[element.dataset.i18nPlaceholder];
  });
  const primaryNav = document.querySelector('.account-nav');
  primaryNav.setAttribute('aria-label', language === 'tr' ? 'Ana gezinme' : 'Primary navigation');
  document.querySelector('.account-modes').setAttribute('aria-label', language === 'tr' ? 'Hesap işlemi' : 'Account action');
  const resetEmail = showingReset ? resetForm.elements.email.value : null;
  setMode(mode);
  if (resetEmail !== null) showReset(resetEmail);
  if (currentUser) renderVerification(currentUser);
}

function showReset(email = authForm.elements.email.value.trim()) {
  showingReset = true;
  authForm.hidden = true;
  resetForm.hidden = false;
  resetForm.elements.email.value = email;
  accountTitle.textContent = message('resetTitle');
  accountIntro.textContent = message('resetIntro');
  clearStatus();
  resetForm.elements.email.focus();
}

function firebaseMessage(error) {
  return words[language][error?.code] ? error.code : 'errorFallback';
}

document.querySelectorAll('[data-mode]').forEach(button => button.addEventListener('click', () => setMode(button.dataset.mode)));
languageButton.addEventListener('click', () => {
  language = language === 'tr' ? 'en' : 'tr';
  auth.languageCode = language;
  translatePage();
});
document.getElementById('forgot-password').addEventListener('click', showReset);
document.getElementById('reset-back').addEventListener('click', () => {
  showingReset = false;
  resetForm.hidden = true;
  authForm.hidden = false;
  accountTitle.textContent = message(mode === 'signup' ? 'signupTitle' : 'loginTitle');
  accountIntro.textContent = message(mode === 'signup' ? 'signupIntro' : 'loginIntro');
  clearStatus();
  authForm.elements.email.focus();
});

authForm.addEventListener('submit', async event => {
  event.preventDefault();
  clearStatus();
  if (!authForm.reportValidity()) return;
  const email = authForm.elements.email.value.trim();
  const password = passwordInput.value;
  if (mode === 'signup' && password !== confirmInput.value) {
    setStatus('passwordMismatch', 'error');
    confirmInput.focus();
    return;
  }
  submitButton.disabled = true;
  try {
    if (mode === 'signup') {
      const credential = await createUserWithEmailAndPassword(auth, email, password);
      setStatus('signupSuccess', 'success');
      try {
        await sendEmailVerification(credential.user);
        renderVerification(credential.user, 'verificationSent');
      } catch {
        renderVerification(credential.user, 'verificationError');
      }
    } else {
      await signInWithEmailAndPassword(auth, email, password);
      setStatus('loginSuccess', 'success');
    }
    passwordInput.value = '';
    confirmInput.value = '';
  } catch (error) {
    setStatus(firebaseMessage(error), 'error');
  } finally {
    submitButton.disabled = false;
  }
});

resetForm.addEventListener('submit', async event => {
  event.preventDefault();
  if (!resetForm.reportValidity()) return;
  const button = resetForm.querySelector('[type=submit]');
  button.disabled = true;
  clearStatus();
  try {
    await sendPasswordResetEmail(auth, resetForm.elements.email.value.trim());
    setStatus('resetSuccess', 'success');
  } catch (error) {
    setStatus(firebaseMessage(error), 'error');
  } finally {
    button.disabled = false;
  }
});

document.getElementById('sign-out').addEventListener('click', async event => {
  const button = event.currentTarget;
  button.disabled = true;
  clearStatus();
  try {
    await signOut(auth);
    setStatus('signOutSuccess', 'success');
  } catch (error) {
    setStatus(firebaseMessage(error), 'error');
  } finally {
    button.disabled = false;
  }
});

document.getElementById('resend-verification').addEventListener('click', async event => {
  const button = event.currentTarget;
  button.disabled = true;
  try {
    await sendEmailVerification(auth.currentUser);
    renderVerification(auth.currentUser, 'verificationSent');
  } catch {
    renderVerification(auth.currentUser, 'verificationError');
  } finally {
    button.disabled = false;
  }
});

document.getElementById('refresh-verification').addEventListener('click', async event => {
  const button = event.currentTarget;
  button.disabled = true;
  try {
    await reload(auth.currentUser);
    currentUser = auth.currentUser;
    renderVerification(currentUser);
  } catch {
    const verificationStatus = document.getElementById('verification-status');
    verificationStatus.textContent = message('verificationRefreshError');
    verificationStatus.className = 'verification-status is-error';
  } finally {
    button.disabled = false;
  }
});

onAuthStateChanged(auth, user => {
  currentUser = user;
  views.hidden = Boolean(user);
  signedIn.hidden = !user;
  if (user) {
    document.getElementById('signed-in-email').textContent = user.email || '';
    renderVerification(user);
    clearStatus();
  }
});

translatePage();
