namespace GeoMeteoApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // РАНЬШЕ БЫЛО ТАК:
            // MainPage = new MainPage();

            // ТЕПЕРЬ СТАВИМ ЗАГРУЗКУ ПЕРВОЙ:
            MainPage = new LoadingPage();
        }
    }
}