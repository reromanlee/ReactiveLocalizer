using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace reromanlee.ReactiveLocalizer.Samples.GoogleSheets.Editor
{
    /// <summary>
    /// Talks to the sample's Apps Script web app: pulls every sheet of the spreadsheet, or pushes sheets into it, each
    /// request carrying the secret token the script checks.
    /// </summary>
    /// <remarks>
    /// An Apps Script web app answers a POST with a redirect to where its reply waits, which is fetched with a GET.
    /// Redirects are followed here rather than by the platform, which may resend the POST instead.
    /// </remarks>
    internal static class GoogleSheetsClient
    {
        private const int TimeoutSeconds = 120;
        private const int RedirectLimit = 5;

        /// <summary>Pulls every sheet; <paramref name="done"/> gets them, or null and the reason it failed.</summary>
        public static void Pull(string url, string token, Action<SheetData[], string> done)
        {
            Send(url, new SyncRequest { token = token, action = "pull", sheets = new SheetData[0] }, done);
        }

        /// <summary>Replaces the contents of each sheet in <paramref name="sheets"/>; <paramref name="done"/> gets null on success, else the reason it failed.</summary>
        public static void Push(string url, string token, SheetData[] sheets, Action<string> done)
        {
            Send(url, new SyncRequest { token = token, action = "push", sheets = sheets }, (_, problem) => done(problem));
        }

        private static void Send(string url, SyncRequest request, Action<SheetData[], string> done)
        {
            UnityWebRequest post = new(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(request))) { contentType = "application/json" },
                downloadHandler = new DownloadHandlerBuffer(),
                redirectLimit = 0,
                timeout = TimeoutSeconds
            };
            Run(post, 0, done);
        }

        private static void Run(UnityWebRequest web, int redirects, Action<SheetData[], string> done)
        {
            web.SendWebRequest().completed += _ =>
            {
                long code = web.responseCode;
                string location = web.GetResponseHeader("Location");
                string body = web.downloadHandler?.text;
                string error = web.error;
                web.Dispose();
                if (code >= 300 && code < 400 && !string.IsNullOrEmpty(location))
                {
                    if (redirects >= RedirectLimit)
                    {
                        done(null, "The web app redirected too many times.");
                        return;
                    }
                    UnityWebRequest get = UnityWebRequest.Get(location);
                    get.redirectLimit = 0;
                    get.timeout = TimeoutSeconds;
                    Run(get, redirects + 1, done);
                    return;
                }
                Finish(code, body, error, done);
            };
        }

        private static void Finish(long code, string body, string error, Action<SheetData[], string> done)
        {
            if (code != 200 || string.IsNullOrEmpty(body))
            {
                done(null, $"The web app answered {code}{(string.IsNullOrEmpty(error) ? string.Empty : $" ({error})")}. Check its URL, and that it's deployed for anyone to call.");
                return;
            }
            SyncResponse response;
            try
            {
                response = JsonUtility.FromJson<SyncResponse>(body);
            }
            catch (ArgumentException)
            {
                // Google answers with an HTML page when the deployment needs a sign-in, which isn't a reply of the script.
                done(null, "The web app didn't answer with the script's reply. Deploy it with 'Who has access: Anyone', and use the URL ending in /exec.");
                return;
            }
            if (response == null || !response.ok)
            {
                done(null, response?.error ?? "The web app sent an empty reply.");
                return;
            }
            done(response.sheets ?? new SheetData[0], null);
        }
    }
}
