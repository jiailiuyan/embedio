﻿#if !NET47
namespace Unosquare.Net
{
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Text;
    using System;
    using Labs.EmbedIO;
    using Labs.EmbedIO.Constants;
    using Swan;

    /// <summary>
    /// Represents some System.NET custom extensions
    /// </summary>
    internal static class NetExtensions
    {
        internal const string Tspecials = "()<>@,;:\\\"/[]?={} \t";

        internal static IEnumerable<string> SplitHeaderValue(this string value, params char[] separators)
        {
            var len = value.Length;
            var seps = new string(separators);

            var buff = new StringBuilder(32);
            var escaped = false;
            var quoted = false;

            for (var i = 0; i < len; i++)
            {
                var c = value[i];

                if (c == '"')
                {
                    if (escaped)
                        escaped = false;
                    else
                        quoted = !quoted;
                }
                else if (c == '\\')
                {
                    if (i < len - 1 && value[i + 1] == '"')
                        escaped = true;
                }
                else if (seps.Contains(c))
                {
                    if (!quoted)
                    {
                        yield return buff.ToString();
                        buff.Length = 0;

                        continue;
                    }
                }

                buff.Append(c);
            }

            if (buff.Length > 0)
                yield return buff.ToString();
        }

        internal static string Unquote(this string str)
        {
            var start = str.IndexOf('\"');
            var end = str.LastIndexOf('\"');

            if (start >= 0 && end >= 0)
                str = str.Substring(start + 1, end - 1);

            return str.Trim();
        }

        internal static bool IsData(this byte opcode) => opcode == 0x1 || opcode == 0x2;

        internal static bool IsData(this Opcode opcode) => opcode == Opcode.Text || opcode == Opcode.Binary;

        internal static byte[] InternalToByteArray(this ushort value, Endianness order)
        {
            var bytes = BitConverter.GetBytes(value);
            if (!order.IsHostOrder())
                Array.Reverse(bytes);

            return bytes;
        }

        internal static byte[] InternalToByteArray(this ulong value, Endianness order)
        {
            var bytes = BitConverter.GetBytes(value);
            if (!order.IsHostOrder())
                Array.Reverse(bytes);

            return bytes;
        }

        internal static bool IsControl(this byte opcode) => opcode > 0x7 && opcode < 0x10;

        internal static bool IsReserved(this CloseStatusCode code)
        {
            return code == CloseStatusCode.Undefined ||
                   code == CloseStatusCode.NoStatus ||
                   code == CloseStatusCode.Abnormal ||
                   code == CloseStatusCode.TlsHandshakeFailure;
        }

        /// <summary>
        /// Converts the order of the specified array of <see cref="byte"/> to the host byte order.
        /// </summary>
        /// <returns>
        /// An array of <see cref="byte"/> converted from <paramref name="source"/>.
        /// </returns>
        /// <param name="source">
        /// An array of <see cref="byte"/> to convert.
        /// </param>
        /// <param name="sourceOrder">
        /// One of the <see cref="Endianness"/> enum values, specifies the byte order of
        /// <paramref name="source"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="source"/> is <see langword="null"/>.
        /// </exception>
        internal static byte[] ToHostOrder(this byte[] source, Endianness sourceOrder)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return source.Length > 1 && !sourceOrder.IsHostOrder() ? source.Reverse().ToArray() : source;
        }

        /// <summary>
        /// Determines whether the specified <see cref="Endianness"/> is host (this computer
        /// architecture) byte order.
        /// </summary>
        /// <returns>
        /// <c>true</c> if <paramref name="order"/> is host byte order; otherwise, <c>false</c>.
        /// </returns>
        /// <param name="order">
        /// One of the <see cref="Endianness"/> enum values, to test.
        /// </param>
        internal static bool IsHostOrder(this Endianness order)
        {
            // true: !(true ^ true) or !(false ^ false)
            // false: !(true ^ false) or !(false ^ true)
            return !(BitConverter.IsLittleEndian ^ (order == Endianness.Little));
        }

        /// <summary>
        /// Tries to create a <see cref="Uri"/> for WebSocket with
        /// the specified <paramref name="uriString"/>.
        /// </summary>
        /// <returns>
        /// <c>true</c> if a <see cref="Uri"/> is successfully created; otherwise, <c>false</c>.
        /// </returns>
        /// <param name="uriString">
        /// A <see cref="string"/> that represents a WebSocket URL to try.
        /// </param>
        /// <param name="result">
        /// When this method returns, a <see cref="Uri"/> that represents a WebSocket URL,
        /// or <see langword="null"/> if <paramref name="uriString"/> is invalid.
        /// </param>
        /// <param name="message">
        /// When this method returns, a <see cref="string"/> that represents an error message,
        /// or <see cref="String.Empty"/> if <paramref name="uriString"/> is valid.
        /// </param>
        internal static bool TryCreateWebSocketUri(
            this string uriString, out Uri result, out string message)
        {
            result = null;

            var uri = uriString.ToUri();
            if (uri == null)
            {
                message = "An invalid URI string: " + uriString;
                return false;
            }

            if (!uri.IsAbsoluteUri)
            {
                message = $"Not an absolute URI: {uriString}";
                return false;
            }

            var schm = uri.Scheme;
            if (!(schm == "ws" || schm == "wss"))
            {
                message = $"The scheme part isn\'t \'ws\' or \'wss\': {uriString}";
                return false;
            }

            if (uri.Fragment.Length > 0)
            {
                message = $"Includes the fragment component: {uriString}";
                return false;
            }

            var port = uri.Port;
            if (port == 0)
            {
                message = $"The port part is zero: {uriString}";
                return false;
            }

            result = port != -1
                ? uri
                : new Uri(
                    $"{schm}://{uri.Host}:{(schm == "ws" ? 80 : 443)}{uri.PathAndQuery}");

            message = string.Empty;
            return true;
        }

        internal static bool IsToken(this string value) =>
            value.All(c => c >= 0x20 && c < 0x7f && !Tspecials.Contains(c));

        /// <summary>
        /// Gets the collection of the HTTP cookies from the specified HTTP <paramref name="headers"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="CookieCollection"/> that receives a collection of the HTTP cookies.
        /// </returns>
        /// <param name="headers">
        /// A <see cref="NameValueCollection"/> that contains a collection of the HTTP headers.
        /// </param>
        /// <param name="response">
        /// <c>true</c> if <paramref name="headers"/> is a collection of the response headers;
        /// otherwise, <c>false</c>.
        /// </param>
        internal static CookieCollection GetCookies(this NameValueCollection headers, bool response)
        {
            var name = response ? "Set-Cookie" : Headers.Cookie;
            return headers != null && headers.AllKeys.Contains(name)
                ? CookieCollection.Parse(headers[name], response)
                : new CookieCollection();
        }

        internal static string ToExtensionString(this CompressionMethod method, params string[] parameters)
        {
            if (method == CompressionMethod.None)
                return string.Empty;

            var m = $"permessage-{method.ToString().ToLower()}";

            return parameters == null || parameters.Length == 0 ? m : $"{m}; {string.Join("; ", parameters)}";
        }

        /// <summary>
        /// Determines whether the specified <see cref="NameValueCollection"/> contains the entry with
        /// the specified both <paramref name="name"/> and <paramref name="value"/>.
        /// </summary>
        /// <returns>
        /// <c>true</c> if <paramref name="collection"/> contains the entry with both
        /// <paramref name="name"/> and <paramref name="value"/>; otherwise, <c>false</c>.
        /// </returns>
        /// <param name="collection">
        /// A <see cref="NameValueCollection"/> to test.
        /// </param>
        /// <param name="name">
        /// A <see cref="string"/> that represents the key of the entry to find.
        /// </param>
        /// <param name="value">
        /// A <see cref="string"/> that represents the value of the entry to find.
        /// </param>
        internal static bool Contains(this NameValueCollection collection, string name, string value)
            => collection[name]?.Split(Strings.CommaSplitChar)
                   .Any(val => val.Trim().Equals(value, StringComparison.OrdinalIgnoreCase)) == true;

        /// <summary>
        /// Determines whether the specified <see cref="string"/> contains any of characters in
        /// the specified array of <see cref="char"/>.
        /// </summary>
        /// <returns>
        /// <c>true</c> if <paramref name="value"/> contains any of <paramref name="chars"/>;
        /// otherwise, <c>false</c>.
        /// </returns>
        /// <param name="value">
        /// A <see cref="string"/> to test.
        /// </param>
        /// <param name="chars">
        /// An array of <see cref="char"/> that contains characters to find.
        /// </param>
        internal static bool Contains(this string value, params char[] chars)
            => chars?.Length == 0 || (!string.IsNullOrEmpty(value) && value.IndexOfAny(chars) > -1);

        internal static bool IsCompressionExtension(this string value, CompressionMethod method) =>
            value.StartsWith(method.ToExtensionString());
    }
}
#endif