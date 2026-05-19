namespace QiMata.MobileIoT
{
    public partial class App : Application
    {
        /// <summary>True when the app is running with mock/simulated services rather than real hardware.</summary>
        public static bool IsMockMode { get; internal set; }

        public App()
        {
            InitializeComponent();

            MainPage = new AppShell();
        }
    }
}
