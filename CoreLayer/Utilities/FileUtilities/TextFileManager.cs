using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;

namespace CoreLayer.Utilities.FileUtilities;

public class TextFileManager
{
    public static async Task<string> TextFileAddAsync(string text, string folderLocation, string contentImageLocation, CancellationToken cancellationToken = default)
    {
        try
        {
            // Klasör yolunu temizle (Başındaki/sonundaki / işaretlerini kaldır)
            folderLocation = folderLocation.TrimStart('/', '\\').TrimEnd('/', '\\');

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folderLocation);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }

            var newFileName = Guid.NewGuid() + ".txt";
            var location = Path.Combine(fullPath, newFileName);

            text = await ProcessContentAsync(text, contentImageLocation, cancellationToken);

            await File.WriteAllTextAsync(location, text, cancellationToken);

            // KAYIT FORMATINI STANDARTLAŞTIR: 
            // Her zaman "/BlogContents/dosyaadi.txt" şeklinde döner. 
            // Windows/Linux ayrımı yapmaz çünkü artık hepsini '/' ile birleştiriyoruz.
            return "/" + folderLocation + "/" + newFileName;
        }
        catch
        {
            return null;
        }
    }
    public static async Task<string> ReadTextFileAsync(string folderLocation, int numberOfLetters = 0, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(folderLocation)) return null;

            // 1. Ters slash'leri düzelt ve baştaki fazla slaşleri/ters slaşleri temizle
            folderLocation = folderLocation.Replace("\\", "/").TrimStart('/', '\\');

            // 2. wwwroot ve dosya yolunu güvenli bir şekilde birleştir
            var fileLocation = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folderLocation);

            if (!File.Exists(fileLocation))
            {
                return null;
            }

            using FileStream fileStream = new(fileLocation, FileMode.Open, FileAccess.Read);
            using StreamReader streamReader = new(fileStream);
            string content = await streamReader.ReadToEndAsync(cancellationToken);

            if (numberOfLetters > 0)
            {
                content = Regex.Replace(content, "&nbsp;", " ");
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(content);
                content = htmlDoc.DocumentNode.InnerText;
                if (content.Length > numberOfLetters)
                {
                    content = content[..numberOfLetters];
                }
            }
            return content;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<string> ProcessContentAsync(string content, string contentImageLocation, CancellationToken cancellationToken = default)
    {
        var images = ExtractImageUrlsWithSizes(content);
        foreach (var image in images)
        {
            if (image.Url.Contains(contentImageLocation))
            {
                continue;
            }

            IFormFile newImage = null;

            if (image.IsBase64)
            {
                newImage = await ImageFileManager.GetBase64ImageAsync(image.Base64, contentImageLocation, cancellationToken);
            }
            else
            {
                newImage = await ImageFileManager.DownloadImageAsync(image.Url, cancellationToken);
            }

            if (image.Width == 0 || image.Height == 0)
            {
                using var imageStream = newImage.OpenReadStream();
                using var img = await Image.LoadAsync<Rgba32>(imageStream, cancellationToken);
                if (image.Width == 0)
                {
                    image.Width = img.Width;
                }
                if (image.Height == 0)
                {
                    image.Height = img.Height;
                }
            }

            string fileName = await ImageFileManager.ImageAddAsync(newImage, contentImageLocation, new Size(image.Width, image.Height), cancellationToken: cancellationToken);

            content = content.Replace(image.Url, fileName);
        }

        var dataFilenamePattern = @"\s*data-filename\s*=\s*""[^""]*""";

        var cleanedHtmlContent = Regex.Replace(content, dataFilenamePattern, string.Empty);

        return cleanedHtmlContent;
    }

    private static Regex _imageTagRegex = new Regex(@"<img[^>]+?src=[""'](?<url>[^""']+)[""'][^>]*?(?:width:\s*(?<width>\d+)[^;]*;)?(?:height:\s*(?<height>\d+)[^;]*;)?[^>]*>", RegexOptions.IgnoreCase);
    private static Regex _base64Regex = new Regex(@"^data:image\/[a-zA-Z]+;base64,(?<data>[^;]+)(?:;width:(?<width>\d+))?(?:;height:(?<height>\d+))?$", RegexOptions.IgnoreCase);

    public static List<ImageInfoModel> ExtractImageUrlsWithSizes(string content)
    {
        var matches = _imageTagRegex.Matches(content);
        var imageInfos = new List<ImageInfoModel>();

        int width = 0;
        int height = 0;

        foreach (Match match in matches)
        {
            bool isBase64 = false;
            string base64Data = string.Empty;
            string widthValue = string.Empty;

            if (match.Value.IndexOf("width: ") != -1)
            {
                widthValue = match.Value.Substring(match.Value.IndexOf("width: ") + 7, 5);
            }
            else if (match.Value.IndexOf("width=") != -1)
            {
                int widthStartIndex = match.Value.IndexOf("width=");
                widthValue = match.Value.Substring(match.Value.IndexOf("width=") + 7, 4);
            }

            if (widthValue.Contains("px"))
            {
                widthValue = widthValue.Substring(0, widthValue.IndexOf('p'));
            }

            if (widthValue.Contains('.'))
            {
                widthValue = widthValue.Substring(0, widthValue.IndexOf('.'));
            }
            else if (widthValue.Contains('"'))
            {
                widthValue = widthValue.Substring(0, widthValue.IndexOf('"'));
            }

            int.TryParse(widthValue, out width);

            string heightValue = string.Empty;

            if (match.Value.IndexOf("height: ") != -1)
            {
                int startIndex = match.Value.IndexOf("height: ") + 8;
                heightValue = match.Value.Substring(startIndex, 6);
            }
            else if (match.Value.IndexOf("height=") != -1)
            {
                int heightStartIndex = match.Value.IndexOf("height=");
                heightValue = match.Value.Substring(match.Value.IndexOf("height=") + 8, 4);
            }

            if (heightValue.Contains("px"))
            {
                heightValue = heightValue.Substring(0, heightValue.IndexOf('p'));
            }

            if (heightValue.Contains('.'))
            {
                heightValue = heightValue.Substring(0, heightValue.IndexOf('.'));
            }
            else if (heightValue.Contains('"'))
            {
                heightValue = heightValue.Substring(0, heightValue.IndexOf('"'));
            }

            int.TryParse(heightValue, out height);

            string url = match.Groups["url"].Value;

            if (url.StartsWith("data:image/svg+xml"))
            {
                continue;
            }

            if (url.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                isBase64 = true;
                var base64Match = _base64Regex.Match(url);
                base64Data = base64Match.Groups["data"].Value;
            }

            imageInfos.Add(new ImageInfoModel
            {
                Url = url,
                Width = width,
                Height = height,
                IsBase64 = isBase64,
                Base64 = base64Data
            });
        }

        return imageInfos;
    }

    public static async Task DeleteContentImageFiles(string fileLocation)
    {
        string content = await ReadTextFileAsync(fileLocation);

        if (string.IsNullOrEmpty(content))
        {
            return;
        }

        var matches = _imageTagRegex.Matches(content);

        foreach (Match match in matches)
        {
            string url = match.Groups["url"].Value;

            DeleteFileManager.DeleteFile(url);
        }
    }
}
