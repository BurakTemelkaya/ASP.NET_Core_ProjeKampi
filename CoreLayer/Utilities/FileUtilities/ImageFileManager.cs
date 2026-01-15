using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CoreLayer.Utilities.FileUtilities;

public static class ImageFileManager
{
    public static async Task<string> ImageAddAsync(IFormFile file, string folderLocation, Size size, string fileName = null, CancellationToken cancellationToken = default)
    {
        using var stream = file.OpenReadStream();

        Image<Rgba32> image = await Image.LoadAsync<Rgba32>(stream, cancellationToken);
        if (image == null)
        {
            return null;
        }

        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = size
        }));

        string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folderLocation);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        bool isPng = file.ContentType == "image/png";
        bool hasTransparency = isPng && HasTransparency(image);

        var extension = (isPng && hasTransparency) ? ".png" : ".jpeg";
        var newImageName = fileName == null
            ? Guid.NewGuid() + extension
            : ReplaceCharactersToEnglishCharacters.ReplaceCharacters(fileName) + "-" + Guid.NewGuid() + extension;
        var location = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folderLocation, newImageName);

        using (var outputStream = new FileStream(location, FileMode.Create))
        {
            if (isPng && hasTransparency)
            {
                PngEncoder encoder = new()
                {
                    CompressionLevel = PngCompressionLevel.BestCompression
                };

                await image.SaveAsync(outputStream, encoder, cancellationToken);
            }
            else
            {
                JpegEncoder encoder = new()
                {
                    Quality = 75
                };
                await image.SaveAsync(outputStream, encoder, cancellationToken);
            }
        }

        return "/" + Path.Combine(folderLocation, newImageName).Replace("\\", "/");

    }

    private static bool HasTransparency(Image<Rgba32> image)
    {
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                if (image[x, y].A < 255)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static async Task<Image<Rgba32>> ResizeImage(IFormFile image, Size size, CancellationToken cancellationToken = default)
    {
        try
        {
            using var imageStream = image.OpenReadStream();
            Image<Rgba32> imgToResize = await Image.LoadAsync<Rgba32>(imageStream, cancellationToken);

            imgToResize.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = size
            }));

            return imgToResize;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return null;
        }
    }

    public static async Task<IFormFile> DownloadImageAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpClient client = new();
            var response = await client.GetAsync(imageUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var image = await Image.LoadAsync<Rgba32>(responseStream, cancellationToken);

            var resultStream = new MemoryStream();
            string contentType;
            string fileExtension;
            if (image.Metadata.DecodedImageFormat.DefaultMimeType == "image/png" && HasTransparency(image))
            {
                var encoder = new PngEncoder
                {
                    CompressionLevel = PngCompressionLevel.BestCompression
                };
                await image.SaveAsync(resultStream, encoder, cancellationToken);
                contentType = "image/png";
                fileExtension = ".png";
            }
            else
            {
                var encoder = new JpegEncoder
                {
                    Quality = 75
                };
                await image.SaveAsync(resultStream, encoder, cancellationToken);
                contentType = "image/jpeg";
                fileExtension = ".jpeg";
            }

            resultStream.Position = 0;

            return new FormFile(resultStream, 0, resultStream.Length, "sample" + fileExtension, "test" + fileExtension)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message, cancellationToken);
            return null;
        }
    }

    public static async Task<IFormFile> GetBase64ImageAsync(string base64String, string contentImageLocation, CancellationToken cancellationToken = default)
    {
        try
        {
            var imageBytes = Convert.FromBase64String(base64String);
            var memoryStream = new MemoryStream(imageBytes);
            var image = await Image.LoadAsync<Rgba32>(memoryStream, cancellationToken);

            memoryStream.Position = 0;

            string extension;
            string contentType;

            if (image.Metadata.DecodedImageFormat.DefaultMimeType == "image/png" && HasTransparency(image))
            {
                extension = ".png";
                contentType = "image/png";
            }
            else
            {
                extension = ".jpeg";
                contentType = "image/jpeg";
            }

            var fileName = Guid.NewGuid().ToString() + extension;
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", contentImageLocation);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var filePath = Path.Combine(folderPath, fileName);

            return new FormFile(memoryStream, 0, imageBytes.Length, fileName, fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving base64 image: {ex.Message}", cancellationToken);
            return null;
        }
    }
}
