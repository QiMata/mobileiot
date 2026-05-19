namespace QiMata.MobileIoT.Controls;

public partial class FeatureDescription : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(FeatureDescription), string.Empty);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public FeatureDescription()
    {
        InitializeComponent();
    }
}
