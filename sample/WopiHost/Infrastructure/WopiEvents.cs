#nullable enable
using WopiHost.Abstractions;
using WopiHost.Core.Models;
using WopiHost.Services;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WopiHost.Infrastructure;

/// <summary>
/// WOPI Events handler for customizing WOPI responses
/// </summary>
public static class WopiEvents
{
    // Static service provider to access services
    private static IServiceProvider? _serviceProvider;
    
    /// <summary>
    /// Initialize with service provider
    /// </summary>
    public static void Initialize(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    /// <summary>
    /// Customize CheckFileInfo response with real user information
    /// </summary>
    public static Task<WopiCheckFileInfo> OnGetWopiCheckFileInfo(WopiCheckFileInfoContext context)
    {
        var wopiCheckFileInfo = context.CheckFileInfo;
        var user = context.User;
        
        try
        {
            // Extract user ID from JWT claims
            var userIdClaim = user?.FindFirst("user_id")?.Value;
            
            if (!string.IsNullOrEmpty(userIdClaim) && userIdClaim != "guest" && userIdClaim != "system")
            {
                if (int.TryParse(userIdClaim, out var userId))
                {
                    // Get FileService to query user name from database
                    if (_serviceProvider != null)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var fileService = scope.ServiceProvider.GetService<IFileService>();
                        var logger = scope.ServiceProvider.GetService<ILogger>();
                        
                        if (fileService != null)
                        {
                            try
                            {
                                // Note: We'll need to make this synchronous since the signature doesn't support async
                                // For now, we'll use a simple fallback until we can make it async
                                var userNameTask = fileService.GetUserNameAsync(userId);
                                userNameTask.Wait(); // Not ideal but needed for sync context
                                var userName = userNameTask.Result;
                                
                                if (!string.IsNullOrEmpty(userName))
                                {
                                    wopiCheckFileInfo.UserId = userIdClaim;
                                    wopiCheckFileInfo.UserFriendlyName = userName;
                                    wopiCheckFileInfo.IsAnonymousUser = false;
                                    
                                    logger?.LogInformation($"Set user information for WOPI: UserId={userIdClaim}, UserName={userName}");
                                }
                                else
                                {
                                    logger?.LogWarning($"User ID {userId} found but no name in database, using fallback");
                                    wopiCheckFileInfo.UserId = userIdClaim;
                                    wopiCheckFileInfo.UserFriendlyName = $"User {userIdClaim}";
                                    wopiCheckFileInfo.IsAnonymousUser = false;
                                }
                            }
                            catch (Exception dbEx)
                            {
                                logger?.LogError(dbEx, $"Error querying user name for ID {userId}");
                                wopiCheckFileInfo.UserId = userIdClaim;
                                wopiCheckFileInfo.UserFriendlyName = $"User {userIdClaim}";
                                wopiCheckFileInfo.IsAnonymousUser = false;
                            }
                        }
                        else
                        {
                            Console.WriteLine("FileService not available, using fallback user name");
                            wopiCheckFileInfo.UserId = userIdClaim;
                            wopiCheckFileInfo.UserFriendlyName = $"User {userIdClaim}";
                            wopiCheckFileInfo.IsAnonymousUser = false;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Service provider not initialized, using fallback user name");
                        wopiCheckFileInfo.UserId = userIdClaim;
                        wopiCheckFileInfo.UserFriendlyName = $"User {userIdClaim}";
                        wopiCheckFileInfo.IsAnonymousUser = false;
                    }
                }
                else
                {
                    Console.WriteLine($"Invalid user ID format: {userIdClaim}");
                    SetGuestUser(wopiCheckFileInfo);
                }
            }
            else
            {
                Console.WriteLine("No valid user ID in claims, using guest user");
                SetGuestUser(wopiCheckFileInfo);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting user information for WOPI, falling back to guest: {ex.Message}");
            SetGuestUser(wopiCheckFileInfo);
        }

        // Enable collaboration features for Office Online
        wopiCheckFileInfo.AllowAdditionalMicrosoftServices = true;
        wopiCheckFileInfo.AllowErrorReportPrompt = true;
        wopiCheckFileInfo.SupportsCoauth = true; // Enable real-time collaboration
        wopiCheckFileInfo.SupportsLocks = true;
        wopiCheckFileInfo.SupportsCobalt = false; // Disable Cobalt to fix editing issues
        wopiCheckFileInfo.SupportsUpdate = true;
        
        // Set permissions based on user type
        if (wopiCheckFileInfo.IsAnonymousUser == true || wopiCheckFileInfo.UserId == "guest")
        {
            // Guest users can only view, cannot edit
            wopiCheckFileInfo.UserCanWrite = false;
            wopiCheckFileInfo.UserCanNotWriteRelative = true;
            wopiCheckFileInfo.ReadOnly = true;
        }
        else
        {
            // Authenticated users can edit
            wopiCheckFileInfo.UserCanWrite = true;
            wopiCheckFileInfo.UserCanNotWriteRelative = false;
            wopiCheckFileInfo.ReadOnly = false;
        }
        
        return Task.FromResult(wopiCheckFileInfo);
    }
    
    /// <summary>
    /// Set guest user information
    /// </summary>
    private static void SetGuestUser(WopiCheckFileInfo wopiCheckFileInfo)
    {
        wopiCheckFileInfo.UserId = "guest";
        wopiCheckFileInfo.UserFriendlyName = "Guest User";
        wopiCheckFileInfo.IsAnonymousUser = true;
        wopiCheckFileInfo.UserCanWrite = false;
        wopiCheckFileInfo.UserCanNotWriteRelative = true;
        wopiCheckFileInfo.ReadOnly = true;
    }
}
