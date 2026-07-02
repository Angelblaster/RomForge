using _3DS.Core.Enums;
using System.Globalization;
using System.Windows.Data;

namespace RomForge.Converters;

public class Locale3dsLanguageToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Locale3dsLanguage lang ? lang switch
        {
            Locale3dsLanguage.None => "미선택",
            Locale3dsLanguage.JP => "일본어",
            Locale3dsLanguage.EN => "영어",
            Locale3dsLanguage.FR => "프랑스어",
            Locale3dsLanguage.DE => "독일어",
            Locale3dsLanguage.IT => "이탈리아어",
            Locale3dsLanguage.ES => "스페인어",
            Locale3dsLanguage.ZH => "중국어(간체)",
            Locale3dsLanguage.KO => "한국어",
            Locale3dsLanguage.NL => "네덜란드어",
            Locale3dsLanguage.PT => "포르투갈어",
            Locale3dsLanguage.RU => "러시아어",
            Locale3dsLanguage.TW => "중국어(번체)",
            _ => lang.ToString(),
        } : "미선택";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
