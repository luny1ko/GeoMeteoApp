using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Text.Json;

namespace GeoMeteoApp;

public partial class MainPage : ContentPage
{
    
    private const string YANDEX_API_KEY = "8c9e615d-1c38-4b72-8669-288a9db3e35a";
    public ObservableCollection<string> Cities { get; set; } = new ObservableCollection<string>();
    private static readonly HttpClient _httpClient = CreateHttpClient();
    private bool _isAnimating = false;

  
    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
            Proxy = System.Net.WebRequest.GetSystemWebProxy(),
            UseProxy = true
        };
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("X-Yandex-Weather-Key", YANDEX_API_KEY);

        return client;
    }

    public MainPage()
    {
        InitializeComponent();
        LoadCities();
        CityPicker.ItemsSource = Cities;
        if (Cities.Count > 0) CityPicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
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
        Cities.Add("Солнечногорск");
        Cities.Add("Дубай");
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
        await MainActionButton.ScaleTo(0.95, 50);
        await MainActionButton.ScaleTo(1, 150, Easing.SpringOut);

        if (CityPicker.SelectedItem == null) return;
        string? selectedCity = CityPicker.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(selectedCity)) return;

        _isAnimating = false;
        WeatherIconLabel.CancelAnimations();
        WeatherResultLayout.CancelAnimations();

        if (WeatherResultLayout.IsVisible) await WeatherResultLayout.FadeTo(0, 150);
        WeatherIconLabel.TranslationY = 0;

        UpdateGreeting();
        TempLabel.Text = "0°";
        DescLabel.Text = "ПОИСК СВЯЗИ...";
        AdviceLabel.Text = "Соединение с Яндексом...";
        WeatherIconLabel.Text = "🔮";

        WeatherResultLayout.IsVisible = true;
        WeatherResultLayout.Opacity = 0;
        WeatherResultLayout.Scale = 0.8;
        WeatherResultLayout.TranslationY = 50;

        try
        {
            if (YANDEX_API_KEY == "ВСТАВЬ_СЮДА_СВОЙ_КЛЮЧ")
            {
                throw new Exception("Не указан API-ключ Яндекса в коде!");
            }

           
            double lat = 0, lon = 0;
            bool coordsFound = false;

            var fallback = GetFallbackCoordinates(selectedCity);
            if (fallback != null)
            {
                lat = fallback.Value.Lat;
                lon = fallback.Value.Lon;
                coordsFound = true;
            }
            else
            {
              
                string encodedCity = Uri.EscapeDataString(selectedCity);
                string geoUrl = $"http://geocoding-api.open-meteo.com/v1/search?name={encodedCity}&count=1&language=ru";
                var geoResult = await _httpClient.GetFromJsonAsync<GeoResponse>(geoUrl);

                if (geoResult?.Results != null && geoResult.Results.Length > 0)
                {
                    lat = geoResult.Results[0].Latitude;
                    lon = geoResult.Results[0].Longitude;
                    coordsFound = true;
                }
            }

            if (!coordsFound)
            {
                DescLabel.Text = "НЕ НАЙДЕНО";
                WeatherIconLabel.Text = "❌";
                AdviceLabel.Text = "Не смогли найти координаты этого города.";
            }
            else
            {                
                string latStr = lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string lonStr = lon.ToString(System.Globalization.CultureInfo.InvariantCulture);

                string yandexUrl = $"https://api.weather.yandex.ru/v2/forecast?lat={latStr}&lon={lonStr}&lang=ru_RU";

                var weatherResult = await _httpClient.GetFromJsonAsync<YandexWeatherResponse>(yandexUrl);

                if (weatherResult?.Fact != null)
                {
                    int temp = (int)Math.Round(weatherResult.Fact.Temp);
                    int feelsLike = (int)Math.Round(weatherResult.Fact.FeelsLike);
                    int wind = (int)Math.Round(weatherResult.Fact.WindSpeed);
                    int pressure = (int)Math.Round(weatherResult.Fact.PressureMm);
                    int humidity = (int)Math.Round(weatherResult.Fact.Humidity);
                    string condition = weatherResult.Fact.Condition ?? "clear";

                    TempLabel.Text = $"{temp}°";
                    FeelsLikeLabel.Text = $"{feelsLike}°";
                    WindLabel.Text = $"{wind} м/с";
                    PressureLabel.Text = $"{pressure} мм";
                    HumidityLabel.Text = $"{humidity}%";

                    // Переводим ответ Яндекса на наш язык и иконки
                    DescLabel.Text = GetYandexDescription(condition);
                    WeatherIconLabel.Text = GetYandexIcon(condition);
                    AdviceLabel.Text = GetSmartAdvice(condition, temp, wind);

                    SetBackgroundTheme(condition);

                    _isAnimating = true;
                    StartFloatingAnimation();
                }
            }
        }
        catch (Exception ex)
        {
            DescLabel.Text = "ОШИБКА АПИ";
            WeatherIconLabel.Text = "🔌";
            string realError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            AdviceLabel.Text = $"Детали: {realError}";
        }

        await Task.WhenAll(
            WeatherResultLayout.FadeTo(1, 400),
            WeatherResultLayout.TranslateTo(0, 0, 500, Easing.SpringOut),
            WeatherResultLayout.ScaleTo(1, 500, Easing.SpringOut)
        );
    }

 
    private (double Lat, double Lon)? GetFallbackCoordinates(string city)
    {
        var dict = new Dictionary<string, (double Lat, double Lon)>(StringComparer.OrdinalIgnoreCase)
        {
            { "Москва", (55.7558, 37.6173) },
            { "Солнечногорск", (56.1850, 36.9780) },
            { "Санкт-Петербург", (59.9343, 30.3351) },
            { "Казань", (55.7961, 49.1064) },
            { "Екатеринбург", (55.7887, 49.1221) },
            { "Новосибирск", (55.0084, 82.9357) },
            { "Дубай", (25.2048, 55.2708) },
            { "Токио", (35.6895, 139.6917) },
            { "Лондон", (51.5085, -0.1257) }
        };

        if (dict.ContainsKey(city)) return dict[city];
        return null;
    }

 
    private string GetYandexDescription(string condition) => condition switch
    {
        "clear" => "ЯСНО",
        "partly-cloudy" => "МАЛООБЛАЧНО",
        "cloudy" => "ОБЛАЧНО",
        "overcast" => "ПАСМУРНО",
        "drizzle" => "МОРОСЬ",
        "light-rain" => "НЕБОЛЬШОЙ ДОЖДЬ",
        "rain" => "ДОЖДЬ",
        "moderate-rain" => "СИЛЬНЫЙ ДОЖДЬ",
        "heavy-rain" => "ЛИВЕНЬ",
        "continuous-heavy-rain" => "ДОЛГИЙ ЛИВЕНЬ",
        "showers" => "ЛИВЕНЬ",
        "wet-snow" => "МОКРЫЙ СНЕГ",
        "light-snow" => "НЕБОЛЬШОЙ СНЕГ",
        "snow" => "СНЕГ",
        "snow-showers" => "СНЕГОПАД",
        "hail" => "ГРАД",
        "thunderstorm" => "ГРОЗА",
        "thunderstorm-with-rain" => "ДОЖДЬ С ГРОЗОЙ",
        "thunderstorm-with-hail" => "ГРОЗА С ГРАДОМ",
        _ => "НЕИЗВЕСТНО"
    };

    private string GetYandexIcon(string condition) => condition switch
    {
        "clear" => "☀️",
        "partly-cloudy" => "⛅",
        "cloudy" or "overcast" => "☁️",
        "drizzle" or "light-rain" => "🌦️",
        "rain" or "moderate-rain" or "heavy-rain" or "continuous-heavy-rain" or "showers" => "🌧️",
        "wet-snow" or "light-snow" or "snow" or "snow-showers" => "❄️",
        "thunderstorm" or "thunderstorm-with-rain" or "thunderstorm-with-hail" => "⛈️",
        _ => "🌈"
    };

    private void SetBackgroundTheme(string condition)
    {
        MainGradient.GradientStops.Clear();
        if (condition == "clear" || condition == "partly-cloudy")
        {
            MainGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#2980B9"), 0.0f));
            MainGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#6DD5FA"), 0.5f));
            MainGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#FF7E5F"), 1.0f));
        }
        else if (condition.Contains("rain") || condition == "showers" || condition == "drizzle")
        {
            MainGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#000046"), 0.0f));
            MainGradient.GradientStops.Add(new GradientStop(Color.FromArgb("#1CB5E0"), 1.0f));
        }
        else if (condition.Contains("snow"))
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

    private string GetSmartAdvice(string condition, int temp, int wind)
    {
        var random = new Random();
        string[] pool;

        if (temp < -15) pool = new[] { "🥶 Сиди дома, там дубак!", "🧣 Шапку надень, уши отморозишь." };
        else if (condition.Contains("rain") || condition.Contains("thunderstorm")) pool = new[] { "☔ Зонт — твой лучший друг.", "🌧️ Мокрое дело!" };
        else if (temp > 25) pool = new[] { "🍦 Время мороженого!", "☀️ Жара пошла." };
        else if (wind > 12) pool = new[] { "💨 Осторожно, сдувает!" };
        else pool = new[] { "☀️ Погодка — кайф, иди гулять.", "✨ Твой лучший день — сегодня." };
        return pool[random.Next(pool.Length)];
    }

    private async void StartFloatingAnimation()
    {
        while (_isAnimating)
        {
            await WeatherIconLabel.TranslateTo(0, -15, 1500, Easing.SinInOut);
            await WeatherIconLabel.TranslateTo(0, 0, 1500, Easing.SinInOut);
        }
    }
}

public class YandexWeatherResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("fact")]
    public YandexFact? Fact { get; set; }
}

public class YandexFact
{
    [System.Text.Json.Serialization.JsonPropertyName("temp")]
    public double Temp { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("feels_like")]
    public double FeelsLike { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("wind_speed")]
    public double WindSpeed { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("pressure_mm")]
    public double PressureMm { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("humidity")]
    public double Humidity { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("condition")]
    public string? Condition { get; set; }
}

public class GeoResponse { [System.Text.Json.Serialization.JsonPropertyName("results")] public GeoLocation[]? Results { get; set; } }
public class GeoLocation { [System.Text.Json.Serialization.JsonPropertyName("latitude")] public double Latitude { get; set; } [System.Text.Json.Serialization.JsonPropertyName("longitude")] public double Longitude { get; set; } }