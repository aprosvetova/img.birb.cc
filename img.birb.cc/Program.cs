using Newtonsoft.Json;
using Microsoft.AspNetCore.HttpOverrides;
using System.Web;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

string[] fileTypes = { ".jpg", ".jpeg", ".png", ".gif", ".mp4" };

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseHttpsRedirection();

app.MapGet("/api/img/", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest();
    }

    var form = await request.ReadFormAsync();
    var key = form.ToList().Find(key => key.Key == "api_key");

    if (key.Key is null || UserDB.GetUserFromKey(key.Value) is null) // invalid key
    {
        return Results.Unauthorized();
    }

    return Results.Ok(FileDB.GetDB());
});

app.MapGet("/api/usr/", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest();
    }

    var form = await request.ReadFormAsync();
    var key = form.ToList().Find(key => key.Key == "api_key");

    if (key.Key is null || UserDB.GetUserFromKey(key.Value) is null) // invalid key
    {
        return Results.Unauthorized();
    }

    return Results.Ok(UserDB.GetDB().Select(x => x.UserToDTO()).ToList());
});

app.MapPost("/api/upload", async Task<IResult> (HttpRequest request) =>
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest();
        }

        var form = await request.ReadFormAsync();
        var key = form.ToList().Find(key => key.Key == "api_key");

        if (key.Key is null || UserDB.GetUserFromKey(key.Value) is null) // invalid key
        {
            return Results.Unauthorized();
        }

        var img = form.Files["img"];

        if (img is null || img.Length == 0) // no file or no exention
        {
            Console.WriteLine("Invalid upload");
            return Results.BadRequest();
        }

        string extension = Path.GetExtension(img.FileName);

        if (extension is null || extension.Length == 0 || !fileTypes.Contains(extension)) // invalid extension
        {
            return Results.BadRequest("Invalid filetype or extension");
        }

        Img newFile = new Img();
        
        newFile.NewImg(0, extension);

        using (var stream = System.IO.File.Create("img/" + newFile.filename))
        {
            await img.CopyToAsync(stream);
        }

        Console.WriteLine($"New File: {newFile.filename}");
        return Results.Text("https://img.birb.cc/img/" + newFile.filename);
    });

FileDB.Load();
UserDB.Load();

app.Run();

public static class FileDB
{
    private static string path = "img.json";
    private static List<Img>? db = new List<Img>();

    public static List<Img>? GetDB()
    {
        return db;
    }
    public static Img? Find(string hash)
    {
        return db.Find(file => file.hash == hash);
    }
    public static void Add(Img file)
    {
        if (Find(file.hash) is null)
        {
            db.Add(file);
            Save();
        }
    }
    public static void Load()
    {
        try
        {
            using (StreamReader SR = new StreamReader(path))
            {
                string json = SR.ReadToEnd();

                db = JsonConvert.DeserializeObject<List<Img>>(json);
                Save();
            }
            Console.WriteLine($"Loaded DB of length {db.Count()}");
        }
        catch
        {
            Console.WriteLine($"Unable to load {path}");
        }
    }
    private static void Save()
    {

        try
        {
            using (StreamWriter SW = new StreamWriter(path, false))
            {
                SW.WriteLine(JsonConvert.SerializeObject(db, Formatting.Indented));
                Console.WriteLine($"{path} saved!");
            }
        }
        catch
        {
            Console.WriteLine($"Error saving {path}!");
        }

    }
}

public class Img
{
    static Random random = new Random();

    public string? hash { get; set; }
    public string? filename { get; set; }
    public int UID { get; set; }

    public void NewImg(int uid, string extension)
    {
        this.hash = newHash(8);
        this.filename = this.hash + extension;
        this.UID = uid;

        UserDB.GetUserFromUID(uid).UploadCount++;
        UserDB.Save();

        FileDB.Add(this);
    }

    string newHash(int length)
    {
        string b64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        string hash = "";
        bool used = true;

        while (used)
        {
            hash = String.Empty;
            for (int i = 0; i < length; i++)
            {
                hash += b64[random.Next(b64.Length)];
            }

            if (FileDB.Find(hash) is null) { used = false; }
        }

        return hash;
    }
}

public class User
{
    public string? Username { get; set; }
    public int UID { get; set; }
    public int UploadCount { get; set; } = 0;
    public string? APIKey { get; set; }

    public UserDTO UserToDTO()
    {
        return new UserDTO
        {
            Username = this.Username,
            UID = this.UID,
            UploadCount = this.UploadCount
        };
    }
}

public class UserDTO
{
    public string? Username { get; set; }
    public int UID { get; set; }
    public int UploadCount { get; set; } = 0;
}

public static class UserDB
{
    private static string path = "user.json";
    private static List<User> db = new List<User>();

    public static void Load()
    {
        try
        {
            using (StreamReader SR = new StreamReader(path))
            {
                string json = SR.ReadToEnd();

                db = JsonConvert.DeserializeObject<List<User>>(json);
                Save();
            }
            Console.WriteLine($"Loaded DB of length {db.Count()}");
        }
        catch
        {
            Console.WriteLine($"Unable to load {path}");
        }
    }
    public static void Save()
    {
        try
        {
            using (StreamWriter SW = new StreamWriter(path, false))
            {
                SW.WriteLine(JsonConvert.SerializeObject(db, Formatting.Indented));
                Console.WriteLine($"{path} saved!");
            }
        }
        catch
        {
            Console.WriteLine($"Error saving {path}!");
        }

    }
    public static List<User> GetDB()
    {
        return db;
    }
    public static User? GetUserFromUsername(string username)
    {
        return db.Find(user => user.Username == username);
    }

    public static User? GetUserFromUID(int uid)
    {
        return db.Find(user => user.UID == uid);
    }

    public static User? GetUserFromKey(string key)
    {
        return db.Find(user => user.APIKey == key);
    }
}