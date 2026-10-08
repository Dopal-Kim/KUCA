namespace KucaMemoServer.Services;

/// <summary>
/// 메모 사진을 wwwroot/photos/{메모 id}.{jpg|png} 로 저장하고 지운다.
/// wwwroot 는 UseStaticFiles 가 내려주므로 /photos/{파일 이름} 으로 바로 받을 수 있다.
/// </summary>
public class PhotoStore
{
    /// <summary>사진 크기 상한 (docs/api.md: 10MB 이하)</summary>
    public const long MaxBytes = 10 * 1024 * 1024;

    readonly string directory;

    public PhotoStore(IWebHostEnvironment env)
    {
        string webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        directory = Path.Combine(webRoot, "photos");
        Directory.CreateDirectory(directory);
    }

    /// <summary>
    /// 파일 앞부분(시그니처)으로 JPEG/PNG 를 판별해 확장자를 돌려준다. 둘 다 아니면 null.
    /// 파일 이름이나 Content-Type 은 앱이 마음대로 보낼 수 있어서 믿지 않는다.
    /// </summary>
    public static async Task<string?> DetectExtensionAsync(IFormFile file)
    {
        var head = new byte[8];
        await using var stream = file.OpenReadStream();
        int read = await stream.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false);
        if (read >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
            return "jpg";
        if (read >= 8 && head.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return "png";
        return null;
    }

    /// <summary>사진을 저장하고 photoUrl("/photos/{파일 이름}")을 돌려준다.</summary>
    public async Task<string> SaveAsync(string memoId, string extension, IFormFile file)
    {
        string fileName = $"{memoId}.{extension}";
        await using var output = File.Create(Path.Combine(directory, fileName));
        await file.CopyToAsync(output);
        return $"/photos/{fileName}";
    }

    /// <summary>photoUrl 이 가리키는 파일을 지운다. 없으면 아무 일도 안 한다.</summary>
    public void Delete(string? photoUrl)
    {
        if (string.IsNullOrEmpty(photoUrl))
            return;
        // 경로 조작을 막기 위해 파일 이름만 쓴다.
        string path = Path.Combine(directory, Path.GetFileName(photoUrl));
        if (File.Exists(path))
            File.Delete(path);
    }
}
