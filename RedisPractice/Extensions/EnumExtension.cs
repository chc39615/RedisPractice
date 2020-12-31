using System;
using System.ComponentModel;
using System.Reflection;

namespace RedisPractice.Extensions
{
    public static class EnumExtension
    {
        public static string GetEnumDescription(this Enum enumValue)
        {
            FieldInfo fi = enumValue.GetType().GetField(enumValue.ToString());

            DescriptionAttribute[] attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);

            if(attributes.Length > 0)
            {
                return attributes[0].Description;
            }
            else
            {
                return enumValue.ToString();
            }
        }

    }
}
