using ImageMagick;
using Memoria.Models.Config;
using Microsoft.Extensions.Options;

namespace Memoria.Services;

public interface IImageService
{
    public Task<bool> ProcessAndSaveFile(IFormFile file, Guid id);

    public FileStream? GetImageFile(Guid id);
}

public class ImageService : IImageService
{
    private string StoragePath { get; }
    
    public ImageService(IFileStorageService fileService, IOptions<FileConfig> options) 
    {
        fileService.EnsureStorageDirectoryExists();
        StoragePath = Path.Combine(options.Value.StoragePath, "_images");
    }
    
    private Stream ResizeAndConvertToAvif(Stream filestream, uint width, uint height, uint quality, string speed) {
        using var image = new MagickImage(filestream);
	    
        image.AutoOrient();
	    
        var geometry = new MagickGeometry(width, height)
        {
            IgnoreAspectRatio = false,
            FillArea = true
        };
        image.Resize(geometry);
        image.Crop(width, height, Gravity.Center);
        image.ResetPage();

        image.Format = MagickFormat.Avif;
        image.Quality = quality;
        image.Settings.SetDefine(MagickFormat.Avif, "speed", speed);
            
        image.Strip();
	    
        var fileResultStream = new MemoryStream();

        image.Write(fileResultStream);

        return fileResultStream;
    }

    public async Task<bool> ProcessAndSaveFile(IFormFile file, Guid id)
    {
        var stream = this.ResizeAndConvertToAvif(file.OpenReadStream(), 256, 256, 90, "4");
        
        var finalFileName = Path.Combine(StoragePath, id.ToString("N"), ".avif");

        await using var fileStream = File.Create(finalFileName);
        stream.Position = 0;
        await stream.CopyToAsync(fileStream);
        return true;
    }

    public FileStream? GetImageFile(Guid id)
    {
        var finalFileName = Path.Combine(StoragePath, id.ToString("N"), ".avif");

        if (!File.Exists(finalFileName))
        {
            return null;
        }
        
        return File.OpenRead(finalFileName);
    }
}