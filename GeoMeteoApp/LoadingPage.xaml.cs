using System.Runtime.CompilerServices;

namespace GeoMeteoApp;

public partial class LoadingPage : ContentPage
{
    public LoadingPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // 1. АНИМАЦИЯ ВОСХОДА СОЛНЦА
        // Солнце поднимается (TranslationY) и появляется (Opacity)
        await Task.WhenAll(
            LogoIcon.FadeTo(1, 1000),
            LogoIcon.TranslateTo(0, 0, 1000, Easing.CubicOut),
            LogoIcon.RotateTo(360, 1000, Easing.CubicOut) // Полный оборот
        );

        // 2. ПОЯВЛЕНИЕ ТЕКСТА (Пружина)
        await LogoText.FadeTo(1, 500);
        await LogoText.ScaleTo(1, 800, Easing.SpringOut);

        // 3. ПОКАЗ ИНДИКАТОРА
        _ = Spinner.FadeTo(1, 500);
        await LoadingStatus.FadeTo(1, 500);

        // Имитация бурной деятельности (загрузка данных)
        LoadingStatus.Text = "Калибровка сенсоров...";
        await Task.Delay(800);
        LoadingStatus.Text = "Связь с космосом...";
        await Task.Delay(800);
        // В файле LoadingPage.xaml.cs замени конец метода OnAppearing:

        LoadingStatus.Text = "Готово!";
        await Task.Delay(500);

        // --- КРАСИВЫЙ ВЫХОД ---
        // Схлопываем логотип и гасим экран
        await Task.WhenAll(
            LogoIcon.ScaleTo(1.5, 400, Easing.CubicIn), // Солнце чуть расширяется перед взрывом
            LogoIcon.FadeTo(0, 400),
            LogoText.FadeTo(0, 400),
            Spinner.FadeTo(0, 400),
            LoadingStatus.FadeTo(0, 400)
        );

        // Переходим на главную
        Application.Current.MainPage = new MainPage();
    }
}