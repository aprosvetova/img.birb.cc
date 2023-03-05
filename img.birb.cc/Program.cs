﻿using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;
using System.Text.RegularExpressions;

// TODO:

// make stats filesize counter ignore .html, .js, .css and favicon.png
// make a release
// check foreach loops and use dict instead maybe possibly idk
// admin panel
// key rotation
// invite gen
// comments

string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
        policy =>
        {
            policy.WithOrigins("*").AllowAnyHeader().AllowAnyMethod();
        });
});

WebApplication app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseHttpsRedirection();  // redirect 80 to 443
app.UseDefaultFiles();      // use index.html & index.cs
app.UseStaticFiles();       // enable static file serving
app.UseCors(MyAllowSpecificOrigins);

app.MapPost("/api/img", async Task<IResult> (HttpRequest request) =>
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

    List<Img> images = new List<Img>();
    User user = UserDB.GetUserFromKey(key.Value);

    foreach (var img in FileDB.GetDB())
    {
        if (img.UID == user.UID)
        {
            images.Add(img);
        }
    }

    return Results.Ok(images);
});

app.MapPost("/api/usr", async Task<IResult> (HttpRequest request) =>
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

    return Results.Ok(UserDB.GetDB().Find(uid => uid.UID == UserDB.GetUserFromKey(key.Value).UID)!.UsrToDTO());
});

app.MapPost("/api/usr/new", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest();
    }

    var form = await request.ReadFormAsync();
    var key = form.ToList().Find(key => key.Key == "api_key");

    if (key.Key is null || UserDB.GetUserFromKey(key.Value) is null || !UserDB.GetUserFromKey(key.Value).IsAdmin) // invalid key
    {
        return Results.Unauthorized();
    }

    var username = form.ToList().Find(username => username.Key.ToLower() == "username");
    var UID = form.ToList().Find(UID => UID.Key.ToLower() == "uid");

    string NewUsername;
    int NewUID = 0;
    string NewKey = Hashing.NewHash(40);

    if (string.IsNullOrEmpty(username.Value) || UserDB.GetUserFromUsername(username.Value) is not null)
    {
        return Results.BadRequest("Invalid Username");
    }

    NewUsername = username.Value;

    if (string.IsNullOrEmpty(UID.Value) || UID.Key is null)
    {
        while (UserDB.GetUserFromUID(NewUID) is not null)
        {
            NewUID += 1;
        }
    }
    else
    {
        NewUID = int.Parse(UID.Value);
        if (UserDB.GetUserFromUID(NewUID) is not null)
        {
            return Results.BadRequest("UID Taken");
        }
    }

    while (UserDB.GetUserFromKey(NewKey) is not null)
    {
        NewKey = Hashing.NewHash(40);
    }

    User newUser = new User
    {
        Username = NewUsername,
        UID = NewUID,
        APIKey = Hashing.HashString(NewKey),
    };
    UserDB.AddUser(newUser);

    return Results.Text(NewUsername + ": " + NewKey);
});

app.MapPost("/api/usr/settings", async Task<IResult> (HttpRequest request) =>
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

    var domain = form.ToList().Find(newDomain => newDomain.Key == "domain");
    var dashMsg = form.ToList().Find(dashMsg => dashMsg.Key == "dashMsg");
    var showURL = form.ToList().Find(showURL => showURL.Key == "showURL");

    User user = UserDB.GetUserFromKey(key.Value);

    if (!string.IsNullOrEmpty(showURL.Value) && showURL.Value == "true" || showURL.Value == "false")
    {
        user.ShowURL = System.Convert.ToBoolean(showURL.Value);
    }

    if (!string.IsNullOrEmpty(dashMsg.Value))
    {
        user.DashMsg = Regex.Replace(dashMsg.Value.ToString().Length < 100 ? dashMsg.Value.ToString() : dashMsg.Value.ToString().Substring(0, 100), @"[^\u0020-\u007E]", string.Empty);
    }
    else
    {
        user.DashMsg = null;
    }

    if (!string.IsNullOrEmpty(domain.Value))
    {
        user.Domain = domain.Value;
    }

    UserDB.Save();

    return Results.Accepted();
});

app.MapPost("/api/users", async Task<IResult> (HttpRequest request) =>
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

    if (UserDB.GetUserFromKey(key.Value).IsAdmin) // return private info for admins
    {
        return Results.Ok(UserDB.GetDB().Select(x => x.UsrToDTO()).ToList());
    }

    return Results.Ok(UserDB.GetDB().Select(x => x.UsersToDTO()).ToList());
});

app.MapGet("/api/dashmsg", () => // get one random username + dashmsg 
{
    List<DashDTO> usrlist = new List<DashDTO>();
    foreach (User user in UserDB.GetDB())
    {
        if (!string.IsNullOrEmpty(user.DashMsg))
        {
            usrlist.Add(user.DashToDTO());
        }
    }

    return usrlist.Count == 0 ? Results.NoContent() : Results.Ok(usrlist[Hashing.rand.Next(usrlist.Count)]);
});

app.MapGet("/api/stats", async () =>
{
    Stats stats = new Stats();
    stats.Files = FileDB.GetDB().Count;
    stats.Users = UserDB.GetDB().Count;
    DirectoryInfo dirInfo = new DirectoryInfo(@"wwwroot/");
    stats.Bytes = await Task.Run(() => dirInfo.EnumerateFiles("*", SearchOption.TopDirectoryOnly).Sum(file => file.Length));

    if (FileDB.GetDB().Count > 0) { stats.Newest = FileDB.GetDB().Last().Timestamp; }

    return Results.Ok(stats);
});

app.MapPost("/api/upload", async (http) =>
{
    http.Features.Get<IHttpMaxRequestBodySizeFeature>()!.MaxRequestBodySize = null; // removes max filesize (set max in NGINX, not here)

    if (!http.Request.HasFormContentType)
    {
        http.Response.StatusCode = 400;
        return;
    }

    var form = await http.Request.ReadFormAsync();
    var key = form.ToList().Find(key => key.Key == "api_key");

    if (key.Key is null || UserDB.GetUserFromKey(key.Value) is null) // invalid key
    {
        http.Response.StatusCode = 401;
        return;
    }

    var img = form.Files["img"];

    if (img is null || img.Length == 0) // no file or no file extention
    {
        Log.Warning("Invalid upload");
        http.Response.StatusCode = 400;
        return;
    }

    string extension = Path.GetExtension(img.FileName);

    if (UserDB.GetUserFromKey(key.Value).IsAdmin == false) // only check magic bytes for non-admins
    {
        Stream? stream = new MemoryStream();
        await img.CopyToAsync(stream!);

        if (!Config.HasAllowedMagicBytes(stream!))
        {
            http.Response.StatusCode = 400;
            Log.Warning("illegal filetype");
            return;
        }
    }

    User user = UserDB.GetUserFromKey(key.Value);
    Img newFile = new Img().NewImg(user.UID, extension, img);

    using (var stream = System.IO.File.Create("wwwroot/" + newFile.Filename))
    {
        await img.CopyToAsync(stream);
    }

    Log.Info($"New File: {newFile.Filename}");
    string[] domains = user.Domain!.Split("\r\n");
    string domain = domains[Hashing.rand.Next(domains.Length)];

    await http.Response.WriteAsync($"{(user.ShowURL ? "​" : "")}https://{domain}/" + newFile.Filename); // First "" contains zero-width space
    return;
});

app.MapDelete("/api/delete/{hash}", async Task<IResult> (HttpRequest request, string hash) =>
{
    if (!request.HasFormContentType || string.IsNullOrEmpty(hash))
    {
        return Results.BadRequest();
    }

    var form = await request.ReadFormAsync();
    var key = form.ToList().Find(key => key.Key == "api_key");

    if (key.Key is null || UserDB.GetUserFromKey(key.Value) is null) // invalid key
    {
        return Results.Unauthorized();
    }

    Img deleteFile = FileDB.Find(hash);

    if (deleteFile == null)
    {
        return Results.NotFound();
    }

    if (deleteFile.UID == UserDB.GetUserFromKey(key.Value).UID || UserDB.GetUserFromKey(key.Value).IsAdmin)
    {
        FileDB.Remove(deleteFile);
        return Results.Ok();
    }

    return Results.Unauthorized();
});

app.MapDelete("/api/nuke", async Task<IResult> (HttpRequest request) =>
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

    FileDB.Nuke(UserDB.GetUserFromKey(key.Value));

    return Results.Ok();
});

Log.Initialize();
Config.Load();
FileDB.Load();
UserDB.Load();

app.Run();
