using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ImmoralityGaming.Extensions
{
    public static class StringExtensions
    {
        public enum SplitOn
        {
            Sentence,
            Word,
            Character
        }

        public static string UrlEncode(this string value)
        {
            if (value.IsNullOrEmpty())
            {
                return string.Empty;
            }

            var newValue = value;
            newValue = newValue.Replace("/", "_");
            newValue = newValue.Replace("€", "&euro;");
            newValue = newValue.Replace("&#8364;", "&euro;");

            newValue = newValue.Replace("|", "%T7c");
            newValue = newValue.Replace("<", "%T3c");
            newValue = newValue.Replace(">", "%T3e");

            if (string.IsNullOrEmpty(newValue))
            {
                return value;
            }

            newValue = newValue.Replace("+", "|||");
            newValue = newValue.Replace("-", "+");
            newValue = newValue.Replace("%2d", "+");
            newValue = newValue.Replace("|||", "-");

            newValue = newValue.ToLowerInvariant();

            return newValue;
        }

        public static string StripHtml(this string value, params string[] allowedTags)
        {
            if (value.IsNullOrEmpty())
            {
                return value;
            }

            var newValue = value;

            if (allowedTags != null && allowedTags.Length > 0)
            {
                var acceptable = string.Join("|", allowedTags);
                var stringPattern = "<(?!/?(" + acceptable + ")\\b)[^>]+>";
                newValue = Regex.Replace(newValue, stringPattern, string.Empty);
            }
            else
            {
                newValue = Regex.Replace(newValue, "<.*?>", string.Empty);
            }

            return newValue.RemoveBreaks();
        }

//        public static HtmlString ToHtml(this string value)
//        {
//            return value.IsNullOrEmpty()
//                ? new HtmlString(string.Empty)
//                : new HtmlString(value);
//        }

        public static string ToSqlString(this string value)
        {
            return value.IsNullOrEmpty()
                ? string.Empty
                : value.Replace("'", "''");
        }

        public static string ToJavaScriptString(this string value)
        {
            return value.IsNullOrEmpty()
                ? string.Empty
                : value.Replace("'", "\\'");
        }

        public static string ToCleanString(this string value)
        {
            return value.IsNullOrEmpty()
                ? string.Empty
                : value.Replace("\"", string.Empty).Replace("'", string.Empty);
        }

        public static string ToProper(this string value)
        {
            if (value.IsNullOrEmpty())
            {
                return value;
            }

            var stringArray = value.ToCharArray();
            stringArray[0] = char.ToUpper(stringArray[0]);

            return new string(stringArray);
        }

        public static string ToUpper(this string value, int position)
        {
            if (value.IsNullOrEmpty())
            {
                return value;
            }

            var stringArray = value.ToCharArray();

            if (position != int.MinValue)
            {
                stringArray[position - 1] = char.ToUpper(stringArray[position - 1]);
            }

            return new string(stringArray);
        }

        public static string Split(this string value, SplitOn splitType, int count)
        {
            return value.Split(splitType, count, false);
        }

        public static string Split(this string value, SplitOn splitType, int count, bool ellipses)
        {
            if (value.IsNullOrEmpty())
            {
                return value;
            }

            string[] stringArr;

            var newString = new System.Text.StringBuilder();
            var endReached = false;

            switch (splitType)
            {
                case SplitOn.Sentence:

                    stringArr = value.Split(Convert.ToChar("."));
                    if (count > stringArr.Length)
                    {
                        newString.Append(value);
                        endReached = true;
                    }
                    else
                    {
                        for (var i = 0; i <= count - 1; i++)
                        {
                            newString.Append(stringArr[i] + ".");
                        }
                    }

                    break;

                case SplitOn.Word:

                    stringArr = value.Split(Convert.ToChar(" "));
                    if (count > stringArr.Length)
                    {
                        newString.Append(value);
                        endReached = true;
                    }
                    else
                    {
                        for (var i = 0; i <= count - 1; i++)
                        {
                            newString.Append(stringArr[i] + " ");
                        }
                    }

                    break;

                case SplitOn.Character:

                    newString.Append(value.Substring(0, count));
                    endReached = count > value.Length;

                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (ellipses & !endReached)
            {
                newString.Append("&hellip;");
            }

            return newString.ToString();
        }

        public static bool InArray(this string value, string[] searchArray)
        {
            if (value.IsNullOrEmpty())
            {
                return false;
            }

            if (searchArray == null)
            {
                return false;
            }

            for (var i = 0; i <= searchArray.Length; i++)
            {
                if (searchArray[i] == value)
                {
                    return true;
                }
            }

            return false;
        }

        public static string[] ToStringArray(this string value)
        {
            return value.ToStringArray(", ");
        }

        public static string[] ToStringArray(this string value, string separator)
        {
            return value.IsNullOrEmpty()
                ? new string[0]
                : value.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static int[] ToIntegerArray(this string value)
        {
            return value.ToIntegerArray(", ");
        }

        public static int[] ToIntegerArray(this string value, string separator)
        {
            return value.IsNullOrEmpty()
				? new int[0]
                : value.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => Convert.ToInt32(x))
                .ToArray();
        }

        public static string Unaccent(this string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var toReplace = "àèìòùÀÈÌÒÙ äëïöüÄËÏÖÜĻ âêîôûÂÊÎÔÛ áćéíóúÁĆÉÍÓÚðÐýÝ ãñõÃÑÕšŠžŽçÇåÅøØðДЯБЫЯЬБЕЖНАЯНД".ToCharArray();
            var replaceChars = "aeiouAEIOU aeiouAEIOUL aeiouAEIOU aceiouACEIOUdDyY anoANOsSzZcCaAoOoxxxxxxxxxxxxxx".ToCharArray();

            for (var index = 0; index <= toReplace.GetUpperBound(0); index++)
            {
                value = value.Replace(toReplace[index], replaceChars[index]);
            }

            return value;
        }

        public static string FormatAnonymous(this string input, object anonymous)
        {
            foreach (PropertyDescriptor prop in TypeDescriptor.GetProperties(anonymous))
            {
                input = input.Replace("{" + prop.Name + "}", (prop.GetValue(anonymous) ?? "(null)").ToString());
            }

            return input;
        }

        public static string TrimStart(this string value, string text)
        {
            if (value.IsNullOrEmpty())
            {
                return value;
            }

            if (text.IsNullOrEmpty())
            {
                return value;
            }

            return value.Substring(0, text.Length) == text ? value.Substring(0, text.Length) : value;
        }

        public static string TrimEndSlash(this string value)
        {
            return value.IsNullOrEmpty()
                ? string.Empty
                : value.TrimEnd('/');
        }

        public static string MaxLength(this string value, int length)
        {
            return value.MaxLength(length, false);
        }

        public static string MaxLength(this string value, int length, bool ellipses)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= length)
            {
                return value;
            }

            var newValue = value.Substring(0, length);
            if (ellipses)
            {
                newValue += "...";
            }

            return newValue;
        }

        public static bool IsLike(this string value, string text)
        {
            if (value.IsNullOrEmpty())
            {
                return false;
            }

            return value.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        public static string FormatBreaks(this string value)
        {
            return value.IsNullOrEmpty()
                ? string.Empty
                : value.Replace(Environment.NewLine, "<br>");
        }

        public static string RemoveBreaks(this string value)
        {
            return value.IsNullOrEmpty()
                ? string.Empty
                : Regex.Replace(value, @"\t|\n|\r", string.Empty);
        }

        public static bool IsNullOrEmpty(this string value)
        {
            return string.IsNullOrEmpty(value);
        }

        public static bool Contains(this string source, string value)
        {
            return !value.IsNullOrEmpty() && source.Contains(value, false);
        }

        public static bool Contains(this string source, string value, bool ignoreCase)
        {
            if (source == null || source.IsNullOrEmpty())
            {
                return false;
            }

            return source.IndexOf(value, ignoreCase ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture) >= 0;
        }

        public static string NumbersOnly(this string value)
        {
            if (value == null || value.IsNullOrEmpty())
            {
                return string.Empty;
            }

            return Regex.Replace(value, "[^\\d]", string.Empty);
        }

        public static bool IsNumeric(this string value)
        {
            if (value == null || value.IsNullOrEmpty())
            {
                return false;
            }

            var outval = 0;

            return int.TryParse(value.NumbersOnly(), out outval);
        }

        public static string Truncate(this string value)
        {
            if (value == null || value.IsNullOrEmpty())
            {
                return string.Empty;
            }

            return value.Truncate(250);
        }

        public static string Truncate(this string value, int length)
        {
            if (value != null && !value.IsNullOrEmpty())
            {
                return value.Length > length ? value.Substring(0, length) : value;
            }

            return string.Empty;
        }

        public static string GetSlugTitle(this string value)
        {
            if (value == null || value.IsNullOrEmpty())
            {
                return value;
            }

            value = value.Unaccent();

            value = value.Replace(" + ", " ");
            value = value.Replace(" / ", " ");
            value = value.Replace("-", " ");
            value = value.Replace("  ", " ");
            value = value.Replace(":", string.Empty);
            value = value.Replace("!", string.Empty);
            value = value.Replace("?", string.Empty);

            value = value.UrlEncode();

            return value;
        }

        public static string GetSlugUrl(this string value)
        {
            if (value == null || value.IsNullOrEmpty())
            {
                return value;
            }

            return value.Unaccent().UrlEncode();
        }

        public static string GetWellFormedUrl(this string value)
        {
            if (value == null || value.IsNullOrEmpty())
            {
                return value;
            }

            return value.StartsWith("http", StringComparison.InvariantCultureIgnoreCase)
                ? value
                : "http://{value}";
        }

        public static bool IsBase64(this string base64String)
        {
            if (string.IsNullOrEmpty(base64String) || base64String.Length % 4 != 0 || base64String.Contains(" ") || base64String.Contains("\t") || base64String.Contains("\r") || base64String.Contains("\n"))
            {
                return false;
            }

            try
            {
                var result = Convert.FromBase64String(base64String);

                if (result.Count() > 0)
                {

                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

		public static string ReplaceRandom(this string self) 
		{
		    var random = UnityEngine.Random.Range(0, self.Length);
		    var sb = new StringBuilder(self);

		    if (sb[random] == ' ')
		    {
		    	if (sb.Length > random)
		    	{
		    		sb[random + 1] = '_';
		    	}
		    	else if (random > 0)
		    	{
					sb[random - 1] = '_';
		    	}
		    }
		    else
		    {
				sb[random] = '_';
		    }

		    self = sb.ToString();
		    return self;
	    }

        public static string[] CommaSeperatedToArray(this string source, string seperator = ", ")
        {
            if (source == null || !source.Contains(seperator))
            {
                return new string[0];
            }

            return source.Split(new string[] { seperator }, StringSplitOptions.RemoveEmptyEntries).ToArray();
        }

        public static bool IsValidEmail(this string email)
        {
            try
            {
                return ((new System.Net.Mail.MailAddress(email)) != null);
            }
            catch
            {
                return false;
            }
        }

        public static string WithColor(this string text, Color color)
        {
            var hexidecimal = ColorUtility.ToHtmlStringRGB(color);

            return string.Format("<color=#{0}>{1}</color>", hexidecimal, text);
        }

        public static Vector3 ToVector3(this string input)
        {
            input = input.Replace("(", string.Empty);
            input = input.Replace(")", string.Empty);

            string seperator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

            var values = input.Split(',');

            float.TryParse(values[0].Replace(".", seperator), out float x);
            float.TryParse(values[1].Replace(".", seperator), out float y);

            return new Vector3(x, y, 0f);
        }
        // translations extensions
    }
}