// ==========================
// File: DTOs/Requests/LoginRequest.cs
// ==========================
namespace MovieApp.DTOs.Requests;
public record LoginRequest(string Email, string Password);

// ==========================
// File: DTOs/Requests/RegisterRequest.cs
// ==========================
namespace MovieApp.DTOs.Requests;
public class RegisterRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
}

// ==========================
// File: DTOs/Responses/Result.cs
// ==========================
namespace MovieApp.DTOs.Responses;
public class Result<T>
{
    private Result(bool isSuccess, T? data, string? message = null)
    {
        IsSuccess = isSuccess;
        Data = data;
        Message = message;
    }

    public bool IsSuccess { get; init; }
    public string? Message { get; init; }
    public T Data { get; init; }

    public static Result<T> Success(T? data, string? message = null) => new(true, data, message);
    public static Result<T> Error(T? data, string? message = null) => new(false, data, message);
}

// ==========================
// File: Models/IEntity.cs
// ==========================
namespace MovieApp.Models;
interface IEntity { }

// ==========================
// File: Models/User.cs
// ==========================
using System;
namespace MovieApp.Models;
public class User : IEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string PasswordHash { get; set; } // Хранение хеша
}

// ==========================
// File: Services/Abstractions/IAccountService.cs
// ==========================
using MovieApp.DTOs.Requests;
using MovieApp.DTOs.Responses;
using MovieApp.Models;
namespace MovieApp.Services.Abstractions;
public interface IAccountService
{
    Result<User> RegisterUser(RegisterRequest request);
    Result<User> LoginUser(LoginRequest request);
}

// ==========================
// File: Services/Abstractions/IDataService.cs
// ==========================
using MovieApp.Models;
namespace MovieApp.Services.Abstractions;
public interface IDataService
{
    void AddData<T>(T data) where T : IEntity;
    IEnumerable<T>? GetAllData<T>() where T : IEntity;
}

// ==========================
// File: Services/Utils/PasswordHasher.cs
// ==========================
using System;
using System.Security.Cryptography;
using System.Text;
namespace MovieApp.Services.Utils;
public static class PasswordHasher
{
    public static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public static bool VerifyPassword(string password, string hashed)
    {
        return HashPassword(password) == hashed;
    }
}

// ==========================
// File: Services/Implementations/AccountService.cs
// ==========================
using MovieApp.DTOs.Requests;
using MovieApp.DTOs.Responses;
using MovieApp.Models;
using MovieApp.Services.Abstractions;
using MovieApp.Services.Utils;
using System.Linq;
namespace MovieApp.Services.Implementations;
class AccountService : IAccountService
{
    private readonly IDataService _dataService;
    public AccountService(IDataService dataService) => _dataService = dataService;

    public Result<User> RegisterUser(RegisterRequest request)
    {
        if (request.Password != request.ConfirmPassword)
            return Result<User>.Error(null, "Passwords do not match");

        var users = _dataService.GetAllData<User>()?.ToList() ?? new List<User>();
        if (users.Any(u => u.Email == request.Email))
            return Result<User>.Error(null, "Email already exists");

        var hashedPassword = PasswordHasher.HashPassword(request.Password);
        var user = new User() { Email = request.Email, PasswordHash = hashedPassword };

        _dataService.AddData(user);
        return Result<User>.Success(user, "User registered successfully");
    }

    public Result<User> LoginUser(LoginRequest request)
    {
        var users = _dataService.GetAllData<User>()?.ToList();
        if (users == null)
            return Result<User>.Error(null, "No users registered");

        var user = users.FirstOrDefault(u => u.Email == request.Email);
        if (user == null || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
            return Result<User>.Error(null, "Invalid email or password");

        return Result<User>.Success(user, "Login successful");
    }
}

// ==========================
// File: Services/Implementations/DataService.cs
// ==========================
using MovieApp.Models;
using MovieApp.Services.Abstractions;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace MovieApp.Services.Implementations;
class DataService : IDataService
{
    public void AddData<T>(T data) where T : IEntity
    {
        var values = GetAllData<T>()?.ToList() ?? new List<T>();
        values.Add(data);

        var json = JsonSerializer.Serialize(values);
        File.WriteAllText($"{typeof(T).Name}sData.json", json);
    }

    public IEnumerable<T>? GetAllData<T>() where T : IEntity
    {
        var path = $"{typeof(T).Name}sData.json";
        if (!File.Exists(path)) return null;

        var json = File.ReadAllText(path);
        if (string.IsNullOrEmpty(json)) return null;

        return JsonSerializer.Deserialize<IEnumerable<T>>(json);
    }
}

// ==========================
// File: Services/Abstractions/INavigationService.cs
// ==========================
using GalaSoft.MvvmLight;
using MovieApp.Messages;
namespace MovieApp.Services.Abstractions;
interface INavigationService
{
    void NavigateTo<T>(NavigationMessage message) where T : ViewModelBase;
}

// ==========================
// File: Messages/NavigationMessage.cs
// ==========================
using GalaSoft.MvvmLight;
namespace MovieApp.Messages;
class NavigationMessage(Type viewModelType, object? data = null)
{
    public Type ViewModelType = viewModelType;
    public object? Data = data;
}

// ==========================
// File: ViewModels/LoginViewModel.cs
// ==========================
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MovieApp.DTOs.Requests;
using MovieApp.Services.Abstractions;
using System.Windows;
namespace MovieApp.ViewModels;
class LoginViewModel : ViewModelBase
{
    private readonly IAccountService _accountService;
    private readonly INavigationService _navigationService;

    public LoginRequest Login { get; set; } = new("", "");

    public LoginViewModel(IAccountService accountService, INavigationService navigationService)
    {
        _accountService = accountService;
        _navigationService = navigationService;
    }

    public RelayCommand LoginCommand => new(() =>
    {
        var result = _accountService.LoginUser(Login);

        if (!result.IsSuccess)
        {
            MessageBox.Show(result.Message ?? "Login failed", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MessageBox.Show($"Welcome, {result.Data.Email}!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        _navigationService.NavigateTo<MainViewModel>(null);
    });
});
