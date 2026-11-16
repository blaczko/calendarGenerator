using System.Windows;
using System.Windows.Media;
using CalendarGenerator.App.Services;
using CalendarGenerator.Core.Orders;

namespace CalendarGenerator.App.Windows;

public partial class WhatsAppMessageWindow : Window
{
    private readonly OrderRecord _order;
    private readonly Action<OrderRecord>? _persist;
    private bool _verified;
    private bool _suppressFlagEvents;
    private string _phoneAtVerify = string.Empty;

    public bool DidPersist { get; private set; }

    public WhatsAppMessageWindow(OrderRecord order, Action<OrderRecord>? persist = null)
    {
        InitializeComponent();
        _order = order ?? throw new ArgumentNullException(nameof(order));
        _persist = persist;
        OrderLabel.Text = string.IsNullOrWhiteSpace(_order.OrderId)
            ? _order.FullName
            : $"{_order.OrderId} · {_order.FullName}";
        PhoneBox.Text = _order.Phone ?? string.Empty;
        PhoneBox.TextChanged += (_, _) =>
        {
            UpdateNormalizedHint();
            RefreshVerificationForCurrentPhone(persistClear: true);
        };

        WelcomeMessageText.Text = WhatsAppService.BuildWelcomeMessage(_order);
        ReviewMessageText.Text = WhatsAppService.BuildReviewMessage(_order);
        SyncSentChecksFromOrder();
        UpdateNormalizedHint();
        RestorePersistedVerification();
    }

    private void RestorePersistedVerification()
    {
        if (!IsPersistedVerificationValid())
        {
            SetVerified(false, resetStatus: true, persist: false);
            return;
        }

        _phoneAtVerify = _order.WhatsAppVerifiedPhone;
        SetVerified(true, resetStatus: false, persist: false);
        VerifyStatus.Text = "Korábban ellenőrizve — üzenet küldhető.";
    }

    private bool IsPersistedVerificationValid()
    {
        if (!_order.WhatsAppVerified)
            return false;
        if (!PhoneNumberHelper.TryNormalizeForWhatsApp(PhoneBox.Text, out var currentDigits))
            return false;
        if (string.IsNullOrWhiteSpace(_order.WhatsAppVerifiedPhone))
            return false;
        return string.Equals(_order.WhatsAppVerifiedPhone, currentDigits, StringComparison.Ordinal);
    }

    private void RefreshVerificationForCurrentPhone(bool persistClear)
    {
        if (!_verified)
            return;

        if (!PhoneNumberHelper.TryNormalizeForWhatsApp(PhoneBox.Text, out var currentDigits) ||
            !string.Equals(currentDigits, _phoneAtVerify, StringComparison.Ordinal))
        {
            SetVerified(false, resetStatus: true, persist: persistClear);
        }
    }

    private void SyncSentChecksFromOrder()
    {
        _suppressFlagEvents = true;
        WelcomeSentCheck.IsChecked = _order.WhatsAppWelcomeSent;
        ReviewSentCheck.IsChecked = _order.WhatsAppReviewSent;
        _suppressFlagEvents = false;
    }

    private void UpdateNormalizedHint()
    {
        if (PhoneNumberHelper.TryNormalizeForWhatsApp(PhoneBox.Text, out var digits))
        {
            NormalizedLabel.Text = "WhatsApp formátum: " + PhoneNumberHelper.FormatDisplay(digits);
            NormalizedLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x2F, 0x6B, 0x3A));
        }
        else
        {
            NormalizedLabel.Text = "A szám még nem érvényes WhatsApp formátumhoz.";
            NormalizedLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x4B, 0x2F));
        }
    }

    private void Verify_Click(object sender, RoutedEventArgs e)
    {
        if (!WhatsAppService.TryOpenChat(PhoneBox.Text, message: null, out var error))
        {
            System.Windows.MessageBox.Show(error, "WhatsApp", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ConfirmPanel.Visibility = Visibility.Visible;
        VerifyStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0x4A, 0x40));
        VerifyStatus.Text = "Megnyílt a WhatsApp. Erősítse meg, hogy a beszélgetés elérhető-e.";
    }

    private void ConfirmYes_Click(object sender, RoutedEventArgs e)
    {
        if (!PhoneNumberHelper.TryNormalizeForWhatsApp(PhoneBox.Text, out var digits))
        {
            System.Windows.MessageBox.Show(
                "Érvényes telefonszám szükséges az ellenőrzés mentéséhez.",
                "WhatsApp",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _phoneAtVerify = digits;
        _order.Phone = PhoneBox.Text?.Trim() ?? _order.Phone;
        _order.WhatsAppVerified = true;
        _order.WhatsAppVerifiedPhone = digits;
        SetVerified(true, resetStatus: false, persist: true);
        Dispatcher.BeginInvoke(() =>
        {
            MessagePanel.BringIntoView();
            ContentScroll.ScrollToVerticalOffset(ContentScroll.VerticalOffset + 120);
            WelcomeMessageText.Focus();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ConfirmNo_Click(object sender, RoutedEventArgs e)
    {
        _order.WhatsAppVerified = false;
        _order.WhatsAppVerifiedPhone = string.Empty;
        _phoneAtVerify = string.Empty;
        SetVerified(false, resetStatus: false, persist: true);
        VerifyStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x4B, 0x2F));
        VerifyStatus.Text = "Ez a szám jelenleg nem elérhető WhatsApp-on. Javítsa a telefonszámot, majd ellenőrizze újra.";
    }

    private void SetVerified(bool verified, bool resetStatus, bool persist = false)
    {
        _verified = verified;
        MessagePanel.IsEnabled = verified;
        MessagePanel.Opacity = verified ? 1 : 0.45;

        if (!verified)
        {
            _phoneAtVerify = string.Empty;
            if (persist)
            {
                _order.WhatsAppVerified = false;
                _order.WhatsAppVerifiedPhone = string.Empty;
            }
        }

        if (resetStatus)
        {
            VerifyStatus.Text = string.Empty;
            ConfirmPanel.Visibility = Visibility.Collapsed;
            if (persist)
                PersistOrder();
            return;
        }

        if (verified)
        {
            VerifyStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x1B, 0x7A, 0x3A));
            if (string.IsNullOrWhiteSpace(VerifyStatus.Text) ||
                VerifyStatus.Text.StartsWith("Megnyílt", StringComparison.Ordinal) ||
                VerifyStatus.Text.StartsWith("Ellenőrizve", StringComparison.Ordinal))
            {
                VerifyStatus.Text = "Ellenőrizve — most küldhet üzenetet.";
            }
            ConfirmPanel.Visibility = Visibility.Collapsed;
        }

        if (persist)
            PersistOrder();
    }

    private void SendWelcome_Click(object sender, RoutedEventArgs e) =>
        SendMessage(WelcomeMessageText.Text, markWelcome: true, markReview: false);

    private void SendReview_Click(object sender, RoutedEventArgs e) =>
        SendMessage(ReviewMessageText.Text, markWelcome: false, markReview: true);

    private void SendMessage(string? text, bool markWelcome, bool markReview)
    {
        if (!_verified)
            return;

        if (string.IsNullOrWhiteSpace(text))
        {
            System.Windows.MessageBox.Show("Írjon üzenetet a küldés előtt.", "WhatsApp", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _order.Phone = PhoneBox.Text?.Trim() ?? _order.Phone;

        if (!WhatsAppService.TryOpenChat(PhoneBox.Text, text, out var error))
        {
            System.Windows.MessageBox.Show(error, "WhatsApp", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (markWelcome)
            _order.WhatsAppWelcomeSent = true;
        if (markReview)
            _order.WhatsAppReviewSent = true;

        SyncSentChecksFromOrder();
        PersistOrder();
    }

    private void WelcomeSentCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressFlagEvents) return;
        _order.WhatsAppWelcomeSent = WelcomeSentCheck.IsChecked == true;
        PersistOrder();
    }

    private void ReviewSentCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressFlagEvents) return;
        _order.WhatsAppReviewSent = ReviewSentCheck.IsChecked == true;
        PersistOrder();
    }

    private void PersistOrder()
    {
        try
        {
            _persist?.Invoke(_order);
            DidPersist = true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "Mentés hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = DidPersist;
        Close();
    }
}
