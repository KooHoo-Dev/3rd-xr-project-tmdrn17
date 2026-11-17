using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using CsvHelper.Configuration;
using UnityEngine;
using UnityEngine.Networking;

namespace Jay.FileIO
{
    /// <summary>
    /// TSV 파일을 List&lt;T&gt;로 읽는다. (3rd-3000-Unity-3D의 TSVReader 기반)
    /// Quest(Android) 빌드에서는 StreamingAssets가 APK 안에 묶여 File.Exists로 읽을 수 없으므로
    /// ReadStreamingTable은 UnityWebRequest로 텍스트를 받아 파싱한다.
    /// </summary>
    public static class TSVReader
    {
        private static readonly CsvConfiguration TsvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = "\t",
            Mode = CsvMode.NoEscape,
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            // 프로퍼티뿐 아니라 public 필드도 헤더에 매핑한다.
            MemberTypes = MemberTypes.Properties | MemberTypes.Fields,
        };

        /// <summary>StreamingAssets 폴더 기준 상대 경로(예: "BowUpgrade.tsv")의 TSV를 읽는다. 모든 플랫폼에서 동작.</summary>
        public static List<T> ReadStreamingTable<T>(string relativePath)
        {
            string path = Path.Combine(Application.streamingAssetsPath, relativePath);

            // Android: jar:file://... 경로라 파일 API로 못 읽는다.
            if (path.Contains("://"))
            {
                using UnityWebRequest request = UnityWebRequest.Get(path);
                UnityWebRequestAsyncOperation op = request.SendWebRequest();
                while (op.isDone == false) { } // APK 내부 로컬 파일이라 즉시 끝난다.

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[TSVReader] 파일을 읽지 못했습니다: {path}\n{request.error}");
                    return null;
                }
                return ReadTableFromText<T>(request.downloadHandler.text, path);
            }

            return ReadTable<T>(path);
        }

        /// <summary>절대 경로의 TSV를 읽는다. (에디터·PC 전용)</summary>
        public static List<T> ReadTable<T>(string filePath)
        {
            if (File.Exists(filePath) == false)
            {
                Debug.LogError($"[TSVReader] 파일이 존재하지 않습니다: {filePath}");
                return null;
            }

            try
            {
                using var reader = new StreamReader(filePath);
                return Parse<T>(reader);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TSVReader] {filePath} 로딩 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>이미 읽어 온 TSV 텍스트를 파싱한다.</summary>
        public static List<T> ReadTableFromText<T>(string text, string sourceName = "text")
        {
            try
            {
                using var reader = new StringReader(text);
                return Parse<T>(reader);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TSVReader] {sourceName} 파싱 실패: {ex.Message}");
                return null;
            }
        }

        private static List<T> Parse<T>(TextReader reader)
        {
            using var csv = new CsvReader(reader, TsvConfig);
            return new List<T>(csv.GetRecords<T>());
        }
    }
}
