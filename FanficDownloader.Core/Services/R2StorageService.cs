using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Configuration;
using FanficDownloader.Core.Utils;
public class R2StorageService
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    public R2StorageService(IConfiguration configuration)
    {
        var endpoint = configuration["R2:Endpoint"];
        var accessKey = configuration["R2:AccessKey"];
        var secretKey = configuration["R2:SecretKey"];
        _bucket = configuration["R2:Bucket"]!;

        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true
        };
        _s3 = new AmazonS3Client(accessKey, secretKey, config);
    }

    public async Task UploadFileAsync(string objectKey, Stream fileStream, string contentType)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = objectKey,
            InputStream = fileStream,
            ContentType = contentType,
            DisablePayloadSigning = true
        };

        await _s3.PutObjectAsync(request);
    }

    public string BuildObjectKey(string url, string format, string title)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();

        var hashBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(url));

        var hash = Convert.ToHexString(hashBytes).ToLower();

        var safeTitle = FileNameHelper.BuildSafeFileName(title, format);
        return $"fanfics/{hash}/{safeTitle}";
    }
    public async Task DownloadFileAsync(string objectKey, Stream targetStream)
    {
        var request = new GetObjectRequest
        {
            BucketName = _bucket,
            Key = objectKey
        };

        using var response = await _s3.GetObjectAsync(request);

        await response.ResponseStream.CopyToAsync(targetStream);
    }
}