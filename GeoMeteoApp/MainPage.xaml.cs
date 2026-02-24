using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Text.Json;

namespace GeoMeteoApp;

public partial class MainPage : ContentPage
{
    public ObservableCollection<string> Cities { get; set; } = new ObservableCollection<string>();
    private static readonly HttpClient _httpClient = new HttpClient();
    private bool _isAnimating = false;

    public MainPage()
    {
        InitializeComponent();
        LoadCities();
        CityPicker.ItemsSource = Cities;
        if (Cities.Count > 0) CityPicker.SelectedIndex = 0;
    }

    // --- АНИМАЦИЯ ПОЯВЛЕНИЯ ВСЕГО ИНТЕРФЕЙСА ---
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Если VS всё еще подчеркивает MainContainer, 
        // просто нажми "Перестроить решение", код верный.
        MainContainer.TranslationY = 30;

        await Task.WhenAll(
            MainContainer.FadeTo(1, 800, Easing.CubicOut),
            MainContainer.TranslateTo(0, 0, 800, Easing.CubicOut)
        );

        UpdateGreeting();
    }

    private void UpdateGreeting()
    {
        var hour = DateTime.Now.Hour;
        if (hour >= 5 && hour < 12) GreetingLabel.Text = "Доброе утро!";
        else if (hour >= 12 && hour < 18) GreetingLabel.Text = "Добрый день!";
        else if (hour >= 18 && hour < 23) GreetingLabel.Text = "Добрый вечер!";
        else GreetingLabel.Text = "Доброй ночи!";
    }

    private void LoadCities()
    {
        string savedCitiesJson = Preferences.Default.Get("SavedCitiesList", string.Empty);
        if (!string.IsNullOrEmpty(savedCitiesJson))
        {
            var savedList = JsonSerializer.Deserialize<List<string>>(savedCitiesJson);
            if (savedList != null && savedList.Count > 0)
            {
                foreach (var city in savedList) Cities.Add(city);
                return;
            }
        }
        Cities.Add("Москва");
        Cities.Add("Дубай");
        Cities.Add("Токио");
    }

    private void SaveCities()
    {
        string json = JsonSerializer.Serialize(Cities);
        Preferences.Default.Set("SavedCitiesList", json);
    }

    private async void OnShowAddCityClicked(object sender, EventArgs e)
    {
        await BtnAdd.ScaleTo(0.8, 50);
        await BtnAdd.ScaleTo(1, 200, Easing.SpringOut);
        AddCityLayout.IsVisible = !AddCityLayout.IsVisible;
    }

    private async void OnDeleteCityClicked(object sender, EventArgs e)
    {
        await BtnDelete.ScaleTo(0.8, 50);
        await BtnDelete.ScaleTo(1, 200, Easing.SpringOut);

        if (CityPicker.SelectedItem == null) return;

        // Исправление CS8600 (добавили ?)
        string? selectedCity = CityPicker.SelectedItem.ToString();
        if (string.IsNullOrEmpty(selectedCity)) return;

        bool answer = await DisplayAlert("Удаление", $"Забыть город {selectedCity}?", "Да", "Нет");
        if (answer)
        {
            Cities.Remove(selectedCity);
            SaveCities();
            if (Cities.Count > 0) CityPicker.SelectedIndex = 0;
            else WeatherResultLayout.IsVisible = false;
        }
    }

    private void OnAddCityConfirmClicked(object sender, EventArgs e)
    {
        string? newCity = NewCityEntry.Text?.Trim();
        if (!string.IsNullOrEmpty(newCity))
        {
            newCity = char.ToUpper(newCity[0]) + newCity.Substring(1).ToLower();
            if (!Cities.Contains(newCity))
            {
                Cities.Add(newCity);
                SaveCities();
            }
            CityPicker.SelectedItem = newCity;
            NewCityEntry.Text = string.Empty;
            AddCityLayout.IsVisible = false;
        }
    }

    private async void OnGetWeatherClicked(object sender, EventArgs e)
    {
        await MainActionButton.ScaleTo(0.9, 50);
        await MainActionButton.ScaleTo(1, 300, Easing.SpringOut);

        if (CityPicker.SelectedItem == null) return;
        string? selectedCity = CityPicker.SelectedItem?.ToString();

        _isAnimating = false;
        WeatherIconLabel.CancelAnimations();
        WeatherIconLabel.TranslationY = 0;
        WeatherIconLabel.RotationY = 0;

        if (WeatherResultLayout.IsVisible) _ = WeatherResultLayout.FadeTo(0, 150);

        UpdateGreeting();
        TempLabel.Text = "0°";
        DescLabel.Text = "АНАЛИЗ...";
        AdviceLabel.Text = "...";
        WeatherIconLabel.Text = "🔮";

        WeatherResultLayout.IsVisible = true;
        WeatherResultLayout.Opacity = 0;
        WeatherResultLayout.RotationX = -90;
        WeatherResultLayout.TranslationY = 100;

        try
        {
            string geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={selectedCity}&count=1&language=ru";
            var geoResult = await _httpClient.GetFromJsonAsync<GeoResponse>(geoUrl);

            if (geoResult?.Results == null || geoResult.Results.Length == 0)
            {
                DescLabel.Text = "НЕ НАЙДЕНО";
                WeatherIconLabel.Text = "❌";
                AdviceLabel.Text = "Попробуйте ввести другое название.";
            }
            else
            {
                double lat = geoResult.Results[0].Latitude;
                double lon = geoResult.Results[0].Longitude;
                string latStr = lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string lonStr = lon.ToString(System.Globalization.CultureInfo.InvariantCulture);

                string weatherUrl = $"https://api.open-meteo.com/v1/forecast?latitude={latStr}&longitude={lonStr}&current=temperature_2m,relative_humidity_2m,surface_pressure,weather_code,apparent_temperature,wind_speed_10m";
                var weatherResult = await _httpClient.GetFromJsonAsync<WeatherResponse>(weatherUrl);

                if (weatherResult?.Current != null)
                {
                    int code = weatherResult.Current.WeatherCode;
                    int pressure = (int)(weatherResult.Current.SurfacePressure * 0.750062);
                    int temp = (int)Math.Round(weatherResult.Current.Temperature);
                    int feelsLike = (int)Math.Round(weatherResult.Current.ApparentTemperature);
                    int wind = (int)Math.Round(weatherResult.Current.WindSpeed);

                    TempLabel.Text = $"{temp}°";
                    FeelsLikeLabel.Text = $"{feelsLike}°";
                    WindLabel.Text = $"{wind} м/с";
                    PressureLabel.Text = $"{pressure} мм";
                    HumidityLabel.Text = $"{weatherResult.Current.RelativeHumidity}%";

                    DescLabel.Text = GetWeatherDescription(code);
                    WeatherIconLabel.Text = GetWeatherIcon(code);
                    AdviceLabel.Text = GetSmartAdvice(code, temp, wind);

                    SetBackgroundTheme(code);

                    _isAnimating = true;
                    StartFloatingAnimation();
                }
            }
        }
        catch (Exception)
        {
            DescLabel.Text = "НЕТ СЕТИ";
            WeatherIconLabel.Text = "🔌";
            AdviceLabel.Text = "Проверьте интернет.";
        }

        await Task.WhenAll(
            WeatherResultLayout.FadeTo(1, 600),
            WeatherResultLayout.TranslateTo(0, 0, 600, Easing.SpringOut),
            WeatherResultLayout.RotateXTo(0, 600, Easing.SpringOut)
        );
    }

    private string GetSmartAdvice(int code, int temp, int wind)
    {
        var random = new Random();
        string[] advicePool;

        // 1. Экстремальный холод
        if (temp < -20)
        {
            advicePool = new[] {
            "🥶 Не выходи из комнаты, не совершай ошибку!",
            "🧊 Официально: на улице морозилка. Сиди дома.",
            "🐧 Даже пингвины сегодня в шоке. Утепляйся максимально!",
            "🔥 Твоя единственная цель на сегодня — горячий чай и плед."
        };
        }
        // 2. Просто холодно
        else if (temp < -5)
        {
            advicePool = new[] {
            "🧣 Шапка, шарф и варежки — твои лучшие друзья сегодня.",
            "☕ Идеальное время для двойного латте и теплого свитера.",
            "🚶 Пробежка до метро засчитывается за кардио.",
            "👂 Уши отморозишь! Надень шапку, мама была права."
        };
        }
        // 3. Жара
        else if (temp > 27)
        {
            advicePool = new[] {
            "🥤 Пей больше воды и старайся держаться тени.",
            "🍦 Официальное разрешение на поедание трех порций мороженого получено.",
            "☀️ Солнце сегодня злое. Не забудь SPF, если не хочешь быть как рак.",
            "⛱️ Идеально для пляжа. Или хотя бы для кондиционера."
        };
        }
        // 4. Дождь / Гроза
        else if (code is 61 or 63 or 65 or 80 or 81 or 82 or 95 or 96 or 99)
        {
            advicePool = new[] {
            "☔ Зонт — это не аксессуар, это средство выживания.",
            "🌧️ Отличный повод пересмотреть любимый сериал под шум дождя.",
            "💦 Лужи глубокие, прыгай осторожнее (или нет).",
            "🍵 Погода шепчет: заваривай чай и никуда не иди."
        };
        }
        // 5. Сильный ветер
        else if (wind > 12)
        {
            advicePool = new[] {
            "💨 Осторожно, сдувает! Держись за столбы.",
            "🪁 Идеально для запуска змея, но плохо для твоей прически.",
            "🧥 Надень что-то непродуваемое, иначе будешь как парус.",
            "🌪️ Ветер сегодня с характером. Будь аккуратнее на поворотах."
        };
        }
        // 6. Снег
        else if (code is 71 or 73 or 75 or 85 or 86)
        {
            advicePool = new[] {
            "❄️ Время лепить снеговика и играть в снежки!",
            "📸 Посмотри, как красиво! Пора сделать пару фото.",
            "🎿 Лыжи сами себя не выгуляют. Пора в парк!",
            "🧸 Снег — это просто бесплатное конфетти от природы. Наслаждайся."
        };
        }
        // 7. Идеальная погода (Ясно/Облачно и тепло)
        else if (temp > 15 && temp <= 27 && code <= 3)
        {
            advicePool = new[] {
            "☀️ Кайфовая погода! Бросай всё и иди гулять.",
            "🚲 Самое время для велосипеда или самоката.",
            "🧘 Воздух — кайф. Можно даже помедитировать в парке.",
            "✨ Сегодня твой день. Погода на твоей стороне!"
        };
        }
        // 8. Обычная серая погода
        else
        {
            advicePool = new[] {
            "🌥️ Обычный день. Не забудь хорошее настроение!",
            "🧥 Накинь куртку, лишним не будет.",
            "🌈 Жизнь не только в погоде, она внутри тебя. Улыбнись!",
            "🥞 Погода так себе, зато отличный повод приготовить блинчики."
        };
        }

        // Возвращаем случайную фразу из выбранного набора
        return advicePool[random.Next(advicePool.Length)];
    }

    private async void StartFloatingAnimation()
    {
        while (_isAnimating)
        {
            await WeatherIconLabel.TranslateTo(0, -15, 1500, Easing.SinInOut);
            await WeatherIconLabel.TranslateTo(0, 0, 1500, Easing.SinInOut);
        }
    }

    private void SetBackgroundTheme(int code)
    {
        MainGradient.GradientStops.Clear();
        if (code == 0) // Ясно
        {
            MainGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#2980B9"), 0.0f));
            MainGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#6DD5FA"), 0.5f));
            MainGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#FF7E5F"), 1.0f));
        }
        else if (code is 1 or 2 or 3) // Облачно
        {
            MainGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#2C3E50"), 0.0f));
            MainGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#4CA1AF"), 1.0f));
        }
        else if (code is 61 or 63 or 65 or 80 or 81 or 82) // Дождь
        {
            MainGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#000046"), 0.0f));
            MainGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#1CB5E0"), 1.0f));
        }
        else if (code is 71 or 73 or 75 or 85 or 86) // Снег
        {
            MainGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#83a4d4"), 0.0f));
            MainGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#b6fbff"), 1.0f));
        }
        else
        {
            MainGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#4A00E0"), 0.0f));
            MainGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#8E2DE2"), 0.5f));
            MainGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#fc6767"), 1.0f));
        }
    }

    private string GetWeatherDescription(int code) => code switch
    {
        0 => "ЯСНО",
        1 or 2 => "ОБЛАЧНО",
        3 => "ПАСМУРНО",
        45 or 48 => "ТУМАН",
        51 or 53 or 55 => "МОРОСЬ",
        61 or 63 or 65 => "ДОЖДЬ",
        71 or 73 or 75 => "СНЕГ",
        95 or 96 or 99 => "ГРОЗА",
        _ => "НЕИЗВЕСТНО"
    };

    private string GetWeatherIcon(int code) => code switch
    {
        0 => "☀️",
        1 or 2 => "⛅",
        3 => "☁️",
        45 or 48 => "🌫️",
        51 or 53 or 55 => "🌦️",
        61 or 63 or 65 => "🌧️",
        71 or 73 or 75 => "❄️",
        95 or 96 or 99 => "⛈️",
        _ => "🌈"
    };
}

public class GeoResponse { public GeoLocation[]? Results { get; set; } }
public class GeoLocation { public double Latitude { get; set; } public double Longitude { get; set; } }
public class WeatherResponse { public CurrentWeather? Current { get; set; } }
public class CurrentWeather
{
    [System.Text.Json.Serialization.JsonPropertyName("temperature_2m")] public double Temperature { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("relative_humidity_2m")] public int RelativeHumidity { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("surface_pressure")] public double SurfacePressure { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("weather_code")] public int WeatherCode { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("wind_speed_10m")] public double WindSpeed { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("apparent_temperature")] public double ApparentTemperature { get; set; }
}