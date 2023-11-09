using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SQLitePCL;
using SQLitePCL.Ugly;
using Humanizer;

#nullable enable

namespace Hspi.Utils
{
    internal static class ExceptionHelper
    {
        public static string GetFullMessage(this Exception ex)
        {
            return GetFullMessage(ex, Environment.NewLine);
        }

        public static string GetFullMessage(this Exception ex, string eol)
        {
            var list = GetMessageList(ex);

            List<string> results = new();
            foreach (var element in list)
            {
                if (results.Count == 0 || results[^1] != element)
                {
                    results.Add(element);
                }
            }

            return string.Join(eol, results);
        }

        public static bool IsCancelException(this Exception ex)
        {
            return ex is TaskCanceledException or
                   OperationCanceledException or
                   ObjectDisposedException;
        }

        private static List<string> GetMessageList(Exception ex)
        {
            var list = new List<string>();
            switch (ex)
            {
                case AggregateException aggregationException:
                    foreach (var innerException in aggregationException.InnerExceptions)
                    {
                        list.AddRange(GetMessageList(innerException));
                    }

                    break;

                case ugly.sqlite3_exception sqlite3Exception:
                    {
                        string message = string.IsNullOrWhiteSpace(sqlite3Exception.errmsg) ?
                                             raw.sqlite3_errstr(sqlite3Exception.errcode).utf8_to_string() :
                                            sqlite3Exception.errmsg;
                        list.Add(message.Humanize());
                    }

                    break;

                default:
                    {
                        string message = ex.Message.Trim(' ', '\r', '\n');
                        list.Add(message.Humanize());
                        if (ex.InnerException != null)
                        {
                            list.AddRange(GetMessageList(ex.InnerException));
                        }
                    }

                    break;
            }

            return list;
        }
    };
}