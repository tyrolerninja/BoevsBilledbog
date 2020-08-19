using App_Code;
using BlogEngine.Core;
using BlogEngine.Core.Providers;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;

public class UploadController : ApiController
{
    public HttpResponseMessage Post(string action, string dirPath = "")
    {
        WebUtils.CheckRightsForAdminPostPages(false);

        HttpPostedFile file = HttpContext.Current.Request.Files[0];
        action = action.ToLowerInvariant();

        if (file.ContentLength > 0)
        {
            var dirName = $"/{DateTime.Now.ToString("yyyy")}/{DateTime.Now.ToString("MM")}";
            var fileName = new FileInfo(file.FileName).Name; // to work in IE and others

            // iOS sends all images as "image.jpg" or "image.png"
            fileName = fileName.Replace("image.jpg", DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".jpg");
            fileName = fileName.Replace("image.png", DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".png");

            if (!string.IsNullOrEmpty(dirPath))
                dirName = dirPath;

            if (action == "filemgr" || action == "file")
            {
                string[] imageExtentions = { ".jpg", ".png", ".jpeg", ".tiff", ".gif", ".bmp"};
                string[] videoExtentions = { ".mp4" };

                if (imageExtentions.Any(x => fileName.ToLower().Contains(x.ToLower())))
                    action = "image";
                else if (videoExtentions.Any(x => fileName.ToLower().Contains(x.ToLower())))
                    action = "video";
                else
                    action = "file";
            }

            BlogEngine.Core.FileSystem.Directory dir;

            if (action == "profile")
            {
                if (Security.IsAuthorizedTo(Rights.EditOwnUser))
                {
                    // upload profile image
                    dir = BlogService.GetDirectory("/avatars");
                    var dot = fileName.LastIndexOf(".");
                    var ext = dot > 0 ? fileName.Substring(dot) : "";
                    var profileFileName = User.Identity.Name + ext;

                    var imgPath = HttpContext.Current.Server.MapPath(dir.FullPath + "/" + profileFileName);
                    var image = Image.FromStream(file.InputStream);
                    Image thumb = image.GetThumbnailImage(80, 80, () => false, IntPtr.Zero);
                    thumb.Save(imgPath);

                    return Request.CreateResponse(HttpStatusCode.Created, profileFileName);
                }
            }
            if (action == "image")
            {
                if (Security.IsAuthorizedTo(Rights.EditOwnPosts))
                {
                    dir = BlogService.GetDirectory(dirName);
                    var image = Image.FromStream(file.InputStream);
                    var imageAsByteArray = ResizeImage(image, 1000);
                    //image.NormalizeOrientation();
                    var uploaded = BlogService.UploadFile(imageAsByteArray, fileName, dir, true);
                    return Request.CreateResponse(HttpStatusCode.Created, uploaded.AsImage.ImageUrl);
                }
            }
            if (action == "file")
            {
                if (Security.IsAuthorizedTo(Rights.EditOwnPosts))
                {
                    dir = BlogService.GetDirectory(dirName);
                    var uploaded = BlogService.UploadFile(file.InputStream, fileName, dir, true);
                    var retUrl = uploaded.FileDownloadPath + "|" + fileName + " (" + BytesToString(uploaded.FileSize) + ")";
                    return Request.CreateResponse(HttpStatusCode.Created, retUrl);
                }
            }
            if (action == "video")
            {
                if (Security.IsAuthorizedTo(Rights.EditOwnPosts))
                {
                    dir = BlogService.GetDirectory(dirName);

                    UploadVideo(dir.FullPath, file, fileName);

                    return Request.CreateResponse(HttpStatusCode.Created, fileName);
                }
            }
        }
        return Request.CreateResponse(HttpStatusCode.BadRequest);
    }

    #region Private methods
    private static byte[] ResizeImage(Image image, int width)
    {
        using (var ms = new MemoryStream())
        {
            if (image.Width <= 1000)
            {
                image.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }

            var aspectRatio = ((decimal)image.Width / image.Height);
            var height = Convert.ToInt32(Math.Floor(width / aspectRatio));

            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }
            
            destImage.Save(ms, ImageFormat.Jpeg);
            return ms.ToArray();
        }
    }

    private static string BytesToString(long byteCount)
    {
        string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
        if (byteCount == 0)
            return "0" + suf[0];
        long bytes = Math.Abs(byteCount);
        int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        double num = Math.Round(bytes / Math.Pow(1024, place), 1);
        return (Math.Sign(byteCount) * num) + suf[place];
    }

    private static void UploadVideo(string virtualFolder, HttpPostedFile file, string fileName)
    {
        var folder = HttpContext.Current.Server.MapPath(virtualFolder);
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        
        file.SaveAs($"{folder}/{fileName}");
    }

    #endregion
}