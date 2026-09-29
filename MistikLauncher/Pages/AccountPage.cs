using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MistikLauncher.Pages;

public sealed class AccountPage : Page, ILanguagePage
{
    readonly MainWindow _main;
    readonly StackPanel _loginPanel = new();
    readonly StackPanel _sessionPanel = new();
    readonly TextBox _email = PageHelpers.DarkTextBox();
    readonly PasswordBox _password = new();
    readonly TextBlock _emailLabel = Label();
    readonly TextBlock _passwordLabel = Label();
    readonly TextBlock _title = Label();
    readonly TextBlock _intro = Label();
    readonly TextBlock _state = Label();
    readonly TextBlock _account = Label();
    readonly TextBlock _privacy = Label();
    readonly Button _signIn = Button();
    readonly Button _register = Button();
    readonly Button _signOut = Button();
    readonly Button _backup = Button();
    bool _busy;

    public AccountPage(MainWindow main)
    {
        _main = main;
        _email.Background = PageHelpers.HexBrush("#0D1727");
        _email.Foreground = Brushes.White;
        _email.BorderBrush = PageHelpers.HexBrush("#365574");
        _email.BorderThickness = new Thickness(1);
        _email.Padding = new Thickness(11, 8, 11, 8);
        _email.MinHeight = 42;
        _email.FontSize = 14;
        System.Windows.Automation.AutomationProperties.SetName(_email, "Email");
        _password.Background = PageHelpers.HexBrush("#0D1727");
        _password.Foreground = Brushes.White;
        _password.BorderBrush = PageHelpers.HexBrush("#365574");
        _password.BorderThickness = new Thickness(1);
        _password.Padding = new Thickness(11, 8, 11, 8);
        _password.MinHeight = 42;
        System.Windows.Automation.AutomationProperties.SetName(_password, "Password");

        var root = new StackPanel { Margin = new Thickness(32), MaxWidth = 900 };
        var hero = new Border
        {
            Background = ColorThemes.Brush("#192C46"),
            BorderBrush = ColorThemes.Brush("#365574"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(24),
            Margin = new Thickness(0, 0, 0, 18)
        };
        var heroContent = new StackPanel();
        _title.FontSize = 28;
        _title.FontWeight = FontWeights.Bold;
        _title.Foreground = Brushes.White;
        _intro.FontSize = 14;
        _intro.Foreground = PageHelpers.HexBrush("#BDCAD8");
        _intro.Margin = new Thickness(0, 8, 0, 0);
        heroContent.Children.Add(_title);
        heroContent.Children.Add(_intro);
        hero.Child = heroContent;
        root.Children.Add(hero);

        var card = new Border
        {
            Background = PageHelpers.HexBrush("#192C46"),
            BorderBrush = ColorThemes.Brush("#365574"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(22)
        };
        var content = new StackPanel();
        _loginPanel.Children.Add(_emailLabel);
        _loginPanel.Children.Add(_email);
        _loginPanel.Children.Add(_passwordLabel);
        _loginPanel.Children.Add(_password);
        var authButtons = new WrapPanel { Margin = new Thickness(0, 16, 0, 4) };
        _signIn.Margin = new Thickness(0, 0, 10, 8);
        _register.Margin = new Thickness(0, 0, 10, 8);
        authButtons.Children.Add(_signIn);
        authButtons.Children.Add(_register);
        _loginPanel.Children.Add(authButtons);

        _account.FontSize = 16;
        _account.FontWeight = FontWeights.SemiBold;
        _account.Foreground = Brushes.White;
        _signOut.Margin = new Thickness(0, 14, 10, 0);
        _backup.Margin = new Thickness(0, 14, 0, 0);
        _sessionPanel.Children.Add(_account);
        _sessionPanel.Children.Add(_privacy);
        var accountButtons = new WrapPanel();
        accountButtons.Children.Add(_backup);
        accountButtons.Children.Add(_signOut);
        _sessionPanel.Children.Add(accountButtons);

        _state.Foreground = PageHelpers.HexBrush("#ADBED6");
        _state.Margin = new Thickness(0, 14, 0, 0);
        content.Children.Add(_loginPanel);
        content.Children.Add(_sessionPanel);
        content.Children.Add(_state);
        card.Child = content;
        root.Children.Add(card);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        _signIn.Click += async (_, _) => await AuthenticateAsync(register: false);
        _register.Click += async (_, _) => await AuthenticateAsync(register: true);
        _signOut.Click += async (_, _) =>
        {
            try
            {
                await _main.AccountAuth.SignOutAsync();
                _state.Text = Text("Oturum kapatıldı.", "You have signed out.");
            }
            catch (FirebaseAccountAuthException ex) { _state.Text = ErrorText(ex.Code); }
            finally { ShowAccountState(); }
        };
        _backup.Click += async (_, _) => await BackupPreferencesAsync();
        Loaded += async (_, _) => await RestoreSessionAsync();
        RefreshLanguage();
        ShowAccountState();
    }

    public void RefreshLanguage()
    {
        _title.Text = Text("Mıstık Launcher hesabı", "Mıstık Launcher account");
        _intro.Text = Text("Web sitesinde oluşturduğun hesabınla giriş yap.", "Sign in with the account you created on the website.");
        _signIn.Content = Text("Giriş yap", "Sign in");
        _register.Content = Text("Hesap oluştur", "Create account");
        _signOut.Content = Text("Çıkış yap", "Sign out");
        _backup.Content = Text("Ayarları hesaba yedekle", "Back up settings to account");
        _emailLabel.Text = Text("E-posta", "Email");
        _passwordLabel.Text = Text("Parola", "Password");
        _privacy.Text = Text(
            "Yalnızca oyuncu adı, oyun sürümü, RAM, dil ve tema bu hesaba yedeklenir. E-posta parolan ve Minecraft / Ely.by oturum bilgilerin eşitlenmez.",
            "Only player name, game version, RAM, language and theme are backed up. Your email password and Minecraft / Ely.by sign-in details are not synced.");
        _state.Text = _busy ? Text("İşlem sürüyor…", "Working…") : "";
        ShowAccountState();
    }

    async Task RestoreSessionAsync()
    {
        SetBusy(true);
        try
        {
            if (await _main.AccountAuth.RestoreSessionAsync())
                _state.Text = Text("Hesabına giriş yapıldı.", "You are signed in.");
        }
        catch (FirebaseAccountAuthException ex) { _state.Text = ErrorText(ex.Code); }
        catch { _state.Text = Text("Hesap oturumu denetlenemedi.", "Could not check the account session."); }
        finally { SetBusy(false); ShowAccountState(); }
    }

    async Task AuthenticateAsync(bool register)
    {
        string email = _email.Text.Trim();
        string password = _password.Password;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            _state.Text = Text("E-posta ve parola alanlarını doldur.", "Enter your email and password.");
            return;
        }
        if (password.Length < 6)
        {
            _state.Text = Text("Parola en az 6 karakter olmalı.", "Password must be at least 6 characters.");
            return;
        }

        SetBusy(true);
        _state.Text = register ? Text("Hesap oluşturuluyor…", "Creating account…") : Text("Giriş yapılıyor…", "Signing in…");
        try
        {
            if (register) await _main.AccountAuth.RegisterAsync(email, password);
            else await _main.AccountAuth.SignInAsync(email, password);
            _state.Text = Text("Hesabına giriş yapıldı.", "You are signed in.");
            _password.Clear();
        }
        catch (FirebaseAccountAuthException ex) { _state.Text = ErrorText(ex.Code); }
        catch { _state.Text = Text("İşlem tamamlanamadı.", "The request could not be completed."); }
        finally
        {
            _password.Clear();
            SetBusy(false);
            ShowAccountState();
        }
    }

    async Task BackupPreferencesAsync()
    {
        SetBusy(true);
        _state.Text = Text("Ayarlar hesaba yedekleniyor…", "Backing up settings…");
        try
        {
            await _main.AccountAuth.BackupLauncherPreferencesAsync(_main.Config);
            _state.Text = Text("Launcher ayarları hesabına yedeklendi.", "Launcher settings were backed up to your account.");
        }
        catch (FirebaseAccountAuthException ex) { _state.Text = ErrorText(ex.Code); }
        catch { _state.Text = Text("Ayarlar yedeklenemedi.", "Settings could not be backed up."); }
        finally { SetBusy(false); ShowAccountState(); }
    }

    void ShowAccountState()
    {
        var account = _main.AccountAuth.CurrentAccount;
        bool signedIn = account is not null;
        _loginPanel.Visibility = signedIn ? Visibility.Collapsed : Visibility.Visible;
        _sessionPanel.Visibility = signedIn ? Visibility.Visible : Visibility.Collapsed;
        if (account is not null)
            _account.Text = Text("Giriş yapılan hesap: ", "Signed-in account: ") + account.Email;
        _signIn.IsEnabled = _register.IsEnabled = _email.IsEnabled = _password.IsEnabled = !_busy;
        _signOut.IsEnabled = _backup.IsEnabled = !_busy;
    }

    void SetBusy(bool busy)
    {
        _busy = busy;
        ShowAccountState();
    }

    static TextBlock Label(string value = "") => new()
    {
        Text = value,
        FontFamily = new FontFamily("Segoe UI"),
        FontSize = 13,
        Foreground = PageHelpers.HexBrush("#BDCAD8"),
        Margin = new Thickness(0, 10, 0, 6),
        TextWrapping = TextWrapping.Wrap
    };

    static Button Button() => PageHelpers.MkBtn("", "#226DA0");

    static string Text(string turkish, string english) => Localization.Language == "en" ? english : turkish;

    static string ErrorText(string code) => Localization.Language == "en" ? code switch
    {
        "EMAIL_EXISTS" => "Account request failed. Check your details and try again.",
        "EMAIL_NOT_FOUND" or "INVALID_PASSWORD" or "INVALID_LOGIN_CREDENTIALS" => "Email or password is incorrect.",
        "WEAK_PASSWORD" => "Choose a stronger password (at least 6 characters).",
        "INVALID_EMAIL" => "Enter a valid email address.",
        "TOO_MANY_ATTEMPTS_TRY_LATER" => "Too many attempts. Try again later.",
        "OPERATION_NOT_ALLOWED" => "Email and password sign-in is not enabled for this Firebase project.",
        "EMAIL_NOT_VERIFIED" => "Verify your email using the link we sent. Then sign in again. You can resend it at mistiklauncherultra.web.app/hesap/.",
        "NETWORK" => "Could not reach Firebase. Check your connection and try again.",
        "AUTH_REQUIRED" => "Sign in to your account first.",
        "PERMISSION_DENIED" => "Firebase denied access to this account profile.",
        "LOCAL_STORAGE" => "Could not securely remove the saved sign-in. Try again.",
        _ => "Account request failed. Try again."
    } : code switch
    {
        "EMAIL_EXISTS" => "Hesap işlemi tamamlanamadı. Bilgilerini kontrol edip tekrar dene.",
        "EMAIL_NOT_FOUND" or "INVALID_PASSWORD" or "INVALID_LOGIN_CREDENTIALS" => "E-posta veya parola hatalı.",
        "WEAK_PASSWORD" => "Daha güçlü bir parola seç (en az 6 karakter).",
        "INVALID_EMAIL" => "Geçerli bir e-posta adresi gir.",
        "TOO_MANY_ATTEMPTS_TRY_LATER" => "Çok fazla deneme yapıldı. Daha sonra tekrar dene.",
        "OPERATION_NOT_ALLOWED" => "Firebase projesinde e-posta ve parola girişi etkin değil.",
        "EMAIL_NOT_VERIFIED" => "E-postandaki doğrulama bağlantısını açıp yeniden giriş yap. Bağlantıyı mistiklauncherultra.web.app/hesap/ adresinden yeniden gönderebilirsin.",
        "NETWORK" => "Firebase'e ulaşılamadı. Bağlantını denetleyip tekrar dene.",
        "AUTH_REQUIRED" => "Önce hesabına giriş yap.",
        "PERMISSION_DENIED" => "Firebase bu hesap profiline erişimi reddetti.",
        "LOCAL_STORAGE" => "Kaydedilen oturum güvenli şekilde silinemedi. Tekrar dene.",
        _ => "Hesap işlemi tamamlanamadı. Tekrar dene."
    };
}
