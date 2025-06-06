using Newtonsoft.Json;
using System;

namespace CUE4Parse.UE4.Wwise.Objects
{
    public class Setting<T>
    {
        public readonly T SettingType;
        public readonly float SettingValue;

        public Setting(T settingType, float settingValue)
        {
            SettingType = settingType ?? throw new ArgumentNullException(nameof(settingType));
            SettingValue = settingValue;
        }

        public void WriteJson(JsonWriter writer, JsonSerializer serializer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            writer.WritePropertyName(SettingType!.ToString() ?? string.Empty);
            writer.WriteValue(SettingValue);
        }
    }
}
