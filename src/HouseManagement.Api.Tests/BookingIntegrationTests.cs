using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using HouseManagement.Api.Common;
using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Data;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace HouseManagement.Api.Tests;

public class BookingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestJwtKey = "PleaseChangeThisSecretOrSetEnvVar";

    private readonly WebApplicationFactory<Program> _factory;

    public BookingIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousBookingRequest_CreatesBooking_AndManagerCanReadAndConfirm()
    {
        var factory = CreateFactory();
        await SeedServiceAsync(factory);

        var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/bookings", new CreateAnonymousBookingRequest
        {
            ServiceId = 1,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            ContactName = "Jane Client",
            Phone = "+254712345678",
            Email = "jane@example.com",
            Address = new ServiceAddressRequest
            {
                Line1 = "1 Main Street",
                City = "Nairobi",
                Country = "Kenya"
            },
            Notes = "Please arrive early"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>();
        Assert.NotNull(created);
        Assert.NotNull(created.Data);
        Assert.Equal(BookingStatus.Requested, created.Data!.Status);

        var manager = CreateAuthenticatedClient(factory, "manager");
        var detailResponse = await manager.GetAsync($"/api/bookings/{created.Data.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>();
        Assert.Equal(created.Data.Id, detail!.Data!.Id);

        var listResponse = await manager.GetAsync("/api/bookings?status=RequestED");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<BookingDto>>>();
        Assert.Contains(list!.Data!, dto => dto.Id == created.Data.Id);

        var confirmResponse = await manager.PostAsync($"/api/bookings/{created.Data.Id}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var confirmed = await confirmResponse.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>();
        Assert.Equal(BookingStatus.Confirmed, confirmed!.Data!.Status);
    }

    [Fact]
    public async Task AnonymousBookingRequest_NotifiesActiveManager()
    {
        var factory = CreateFactory();
        await SeedServiceAsync(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            db.Users.Add(new User
            {
                Id = 42,
                UserName = "manager",
                Email = "manager@example.com",
                PasswordHash = "hash",
                Role = Roles.Manager,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        var anonymous = factory.CreateClient();
        var createResponse = await anonymous.PostAsJsonAsync("/api/bookings", new CreateAnonymousBookingRequest
        {
            ServiceId = 1,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            ContactName = "Jane Client",
            Phone = "+254712345678",
            Address = new ServiceAddressRequest
            {
                Line1 = "1 Main Street",
                City = "Nairobi",
                Country = "Kenya"
            }
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>();

        var manager = CreateAuthenticatedClient(factory, Roles.Manager, 42);
        var notifications = await manager.GetFromJsonAsync<ApiResponse<List<NotificationDto>>>("/api/notifications/me");

        Assert.NotNull(created!.Data);
        var notification = Assert.Single(notifications!.Data!);
        Assert.Equal(NotificationTypes.BookingCreated, notification.Type);
        Assert.Equal(created.Data.Id, notification.RelatedEntityId);
        Assert.Equal("Booking", notification.RelatedEntityType);
        Assert.Contains(created.Data.Reference, notification.Message);
    }

    [Fact]
    public async Task ManagerCanAssignEligibleAvailableHouseHelp_ToConfirmedBooking()
    {
        var factory = CreateFactory();
        await SeedServiceAsync(factory);

        var scheduledStart = DateTimeOffset.UtcNow.AddDays(2).Date.AddHours(10);
        var scheduledEnd = scheduledStart.AddHours(2);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var houseHelp = new HouseHelp
            {
                Id = 77,
                FirstName = "Grace",
                LastName = "Helper",
                Phone = "+254700000077",
                City = "Nairobi",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                Skills = new List<HouseHelpSkill>
                {
                    new HouseHelpSkill { ServiceName = "Booking Test Service" }
                },
                Availabilities = new List<HouseHelpAvailability>
                {
                    new HouseHelpAvailability
                    {
                        DayOfWeek = scheduledStart.DayOfWeek,
                        StartTime = new TimeOnly(9, 0),
                        EndTime = new TimeOnly(17, 0),
                        IsActive = true
                    }
                }
            };

            db.HouseHelps.Add(houseHelp);
            db.Bookings.Add(new Booking
            {
                Id = 200,
                Reference = "BK-ASSIGN",
                ServiceId = 1,
                ServiceAddress = new ServiceAddress
                {
                    Line1 = "2 Assignment Street",
                    City = "Nairobi",
                    Country = "Kenya"
                },
                ScheduledStart = scheduledStart,
                ScheduledEnd = scheduledEnd,
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var manager = CreateAuthenticatedClient(factory, "manager", 77);
        var response = await manager.PostAsJsonAsync("/api/bookings/200/assign", new AssignHouseHelpRequest { HouseHelpId = 77 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>();
        Assert.NotNull(payload);
        Assert.Equal(BookingStatus.Assigned, payload!.Data!.Status);
        Assert.Equal(77, payload.Data.AssignedByUserId);
        Assert.NotNull(payload.Data.AssignedAt);
    }

    [Fact]
    public async Task AssignRoute_RejectsInactiveHouseHelp()
    {
        var factory = CreateFactory();
        await SeedServiceAsync(factory);

        var scheduledStart = DateTimeOffset.UtcNow.AddDays(2).Date.AddHours(11);
        var scheduledEnd = scheduledStart.AddHours(2);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var houseHelp = new HouseHelp
            {
                Id = 88,
                FirstName = "Inactive",
                LastName = "Helper",
                Phone = "+254700000088",
                City = "Nairobi",
                IsActive = false,
                CreatedAt = DateTimeOffset.UtcNow,
                Skills = new List<HouseHelpSkill>
                {
                    new HouseHelpSkill { ServiceName = "Booking Test Service" }
                },
                Availabilities = new List<HouseHelpAvailability>
                {
                    new HouseHelpAvailability
                    {
                        DayOfWeek = scheduledStart.DayOfWeek,
                        StartTime = new TimeOnly(9, 0),
                        EndTime = new TimeOnly(17, 0),
                        IsActive = true
                    }
                }
            };

            db.HouseHelps.Add(houseHelp);
            db.Bookings.Add(new Booking
            {
                Id = 201,
                Reference = "BK-INACTIVE",
                ServiceId = 1,
                ServiceAddress = new ServiceAddress
                {
                    Line1 = "99 Inactive Road",
                    City = "Nairobi",
                    Country = "Kenya"
                },
                ScheduledStart = scheduledStart,
                ScheduledEnd = scheduledEnd,
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var manager = CreateAuthenticatedClient(factory, "manager");
        var response = await manager.PostAsJsonAsync("/api/bookings/201/assign", new AssignHouseHelpRequest { HouseHelpId = 88 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<object?>>();
        Assert.NotNull(payload);
        Assert.Contains("not active", payload!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssignRoute_RejectsHouseHelpMissingRequiredServiceSkill()
    {
        var factory = CreateFactory();
        await SeedServiceAsync(factory);

        var scheduledStart = DateTimeOffset.UtcNow.AddDays(2).Date.AddHours(12);
        var scheduledEnd = scheduledStart.AddHours(2);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var houseHelp = new HouseHelp
            {
                Id = 89,
                FirstName = "Wrong",
                LastName = "Skill",
                Phone = "+254700000089",
                City = "Nairobi",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                Skills = new List<HouseHelpSkill>
                {
                    new HouseHelpSkill { ServiceName = "Laundry" }
                },
                Availabilities = new List<HouseHelpAvailability>
                {
                    new HouseHelpAvailability
                    {
                        DayOfWeek = scheduledStart.DayOfWeek,
                        StartTime = new TimeOnly(9, 0),
                        EndTime = new TimeOnly(17, 0),
                        IsActive = true
                    }
                }
            };

            db.HouseHelps.Add(houseHelp);
            db.Bookings.Add(new Booking
            {
                Id = 202,
                Reference = "BK-SKILL",
                ServiceId = 1,
                ServiceAddress = new ServiceAddress
                {
                    Line1 = "11 Skill Lane",
                    City = "Nairobi",
                    Country = "Kenya"
                },
                ScheduledStart = scheduledStart,
                ScheduledEnd = scheduledEnd,
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var manager = CreateAuthenticatedClient(factory, "manager");
        var response = await manager.PostAsJsonAsync("/api/bookings/202/assign", new AssignHouseHelpRequest { HouseHelpId = 89 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<object?>>();
        Assert.NotNull(payload);
        Assert.Contains("does not support the requested service", payload!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssignRoute_RejectsHouseHelpOutsideAvailabilityWindow()
    {
        var factory = CreateFactory();
        await SeedServiceAsync(factory);

        var scheduledStart = DateTimeOffset.UtcNow.AddDays(2).Date.AddHours(18);
        var scheduledEnd = scheduledStart.AddHours(2);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var houseHelp = new HouseHelp
            {
                Id = 90,
                FirstName = "Busy",
                LastName = "Helper",
                Phone = "+254700000090",
                City = "Nairobi",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                Skills = new List<HouseHelpSkill>
                {
                    new HouseHelpSkill { ServiceName = "Booking Test Service" }
                },
                Availabilities = new List<HouseHelpAvailability>
                {
                    new HouseHelpAvailability
                    {
                        DayOfWeek = scheduledStart.DayOfWeek,
                        StartTime = new TimeOnly(9, 0),
                        EndTime = new TimeOnly(17, 0),
                        IsActive = true
                    }
                }
            };

            db.HouseHelps.Add(houseHelp);
            db.Bookings.Add(new Booking
            {
                Id = 203,
                Reference = "BK-AVAIL",
                ServiceId = 1,
                ServiceAddress = new ServiceAddress
                {
                    Line1 = "12 Availability Road",
                    City = "Nairobi",
                    Country = "Kenya"
                },
                ScheduledStart = scheduledStart,
                ScheduledEnd = scheduledEnd,
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var manager = CreateAuthenticatedClient(factory, "manager");
        var response = await manager.PostAsJsonAsync("/api/bookings/203/assign", new AssignHouseHelpRequest { HouseHelpId = 90 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<object?>>();
        Assert.NotNull(payload);
        Assert.Contains("not available during the requested service window", payload!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssignRoute_RejectsOverlappingBookingForHouseHelp()
    {
        var factory = CreateFactory();
        await SeedServiceAsync(factory);

        var scheduledStart = DateTimeOffset.UtcNow.AddDays(2).Date.AddHours(14);
        var scheduledEnd = scheduledStart.AddHours(2);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var houseHelp = new HouseHelp
            {
                Id = 91,
                FirstName = "Booked",
                LastName = "Helper",
                Phone = "+254700000091",
                City = "Nairobi",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                Skills = new List<HouseHelpSkill>
                {
                    new HouseHelpSkill { ServiceName = "Booking Test Service" }
                },
                Availabilities = new List<HouseHelpAvailability>
                {
                    new HouseHelpAvailability
                    {
                        DayOfWeek = scheduledStart.DayOfWeek,
                        StartTime = new TimeOnly(9, 0),
                        EndTime = new TimeOnly(20, 0),
                        IsActive = true
                    }
                }
            };

            db.HouseHelps.Add(houseHelp);
            db.Bookings.Add(new Booking
            {
                Id = 204,
                Reference = "BK-OVERLAP-EXISTING",
                ServiceId = 1,
                AssignedHouseHelpId = houseHelp.Id,
                ServiceAddress = new ServiceAddress
                {
                    Line1 = "13 Existing Street",
                    City = "Nairobi",
                    Country = "Kenya"
                },
                ScheduledStart = scheduledStart.AddHours(-1),
                ScheduledEnd = scheduledStart.AddHours(1),
                Status = BookingStatus.Assigned,
                CreatedAt = DateTimeOffset.UtcNow
            });
            db.Bookings.Add(new Booking
            {
                Id = 205,
                Reference = "BK-OVERLAP-NEW",
                ServiceId = 1,
                ServiceAddress = new ServiceAddress
                {
                    Line1 = "14 New Street",
                    City = "Nairobi",
                    Country = "Kenya"
                },
                ScheduledStart = scheduledStart,
                ScheduledEnd = scheduledEnd,
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var manager = CreateAuthenticatedClient(factory, "manager");
        var response = await manager.PostAsJsonAsync("/api/bookings/205/assign", new AssignHouseHelpRequest { HouseHelpId = 91 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<object?>>();
        Assert.NotNull(payload);
        Assert.Contains("already assigned for a conflicting booking", payload!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DoubleBookingPrevention_RejectsSecondAssignmentForSameTimeWindow()
    {
        var factory = CreateFactory();
        await SeedServiceAsync(factory);

        var scheduledStart = DateTimeOffset.UtcNow.AddDays(3).Date.AddHours(15);
        var scheduledEnd = scheduledStart.AddHours(2);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var houseHelp = new HouseHelp
            {
                Id = 92,
                FirstName = "Double",
                LastName = "Helper",
                Phone = "+254700000092",
                City = "Nairobi",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                Skills = new List<HouseHelpSkill>
                {
                    new HouseHelpSkill { ServiceName = "Booking Test Service" }
                },
                Availabilities = new List<HouseHelpAvailability>
                {
                    new HouseHelpAvailability
                    {
                        DayOfWeek = scheduledStart.DayOfWeek,
                        StartTime = new TimeOnly(9, 0),
                        EndTime = new TimeOnly(22, 0),
                        IsActive = true
                    }
                }
            };

            db.HouseHelps.Add(houseHelp);
            db.Bookings.AddRange(
                new Booking
                {
                    Id = 206,
                    Reference = "BK-DOUBLE-1",
                    ServiceId = 1,
                    ServiceAddress = new ServiceAddress
                    {
                        Line1 = "16 First Double Street",
                        City = "Nairobi",
                        Country = "Kenya"
                    },
                    ScheduledStart = scheduledStart,
                    ScheduledEnd = scheduledEnd,
                    Status = BookingStatus.Confirmed,
                    CreatedAt = DateTimeOffset.UtcNow
                },
                new Booking
                {
                    Id = 207,
                    Reference = "BK-DOUBLE-2",
                    ServiceId = 1,
                    ServiceAddress = new ServiceAddress
                    {
                        Line1 = "17 Second Double Street",
                        City = "Nairobi",
                        Country = "Kenya"
                    },
                    ScheduledStart = scheduledStart,
                    ScheduledEnd = scheduledEnd,
                    Status = BookingStatus.Confirmed,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            await db.SaveChangesAsync();
        }

        var manager = CreateAuthenticatedClient(factory, "manager");
        var firstResponse = await manager.PostAsJsonAsync("/api/bookings/206/assign", new AssignHouseHelpRequest { HouseHelpId = 92 });
        var secondResponse = await manager.PostAsJsonAsync("/api/bookings/207/assign", new AssignHouseHelpRequest { HouseHelpId = 92 });

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
    }

    [Fact]
    public async Task AssignRoute_RejectsConcurrentOverlappingAssignments_ForSameHouseHelp()
    {
        var factory = CreateFactory();
        await SeedServiceAsync(factory);

        var scheduledStart = DateTimeOffset.UtcNow.AddDays(4).Date.AddHours(16);
        var scheduledEnd = scheduledStart.AddHours(2);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var houseHelp = new HouseHelp
            {
                Id = 93,
                FirstName = "Concurrent",
                LastName = "Helper",
                Phone = "+254700000093",
                City = "Nairobi",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                Skills = new List<HouseHelpSkill>
                {
                    new HouseHelpSkill { ServiceName = "Booking Test Service" }
                },
                Availabilities = new List<HouseHelpAvailability>
                {
                    new HouseHelpAvailability
                    {
                        DayOfWeek = scheduledStart.DayOfWeek,
                        StartTime = new TimeOnly(9, 0),
                        EndTime = new TimeOnly(22, 0),
                        IsActive = true
                    }
                }
            };

            db.HouseHelps.Add(houseHelp);
            db.Bookings.AddRange(
                new Booking
                {
                    Id = 208,
                    Reference = "BK-CONC-1",
                    ServiceId = 1,
                    ServiceAddress = new ServiceAddress
                    {
                        Line1 = "18 Concurrency Avenue",
                        City = "Nairobi",
                        Country = "Kenya"
                    },
                    ScheduledStart = scheduledStart,
                    ScheduledEnd = scheduledEnd,
                    Status = BookingStatus.Confirmed,
                    CreatedAt = DateTimeOffset.UtcNow
                },
                new Booking
                {
                    Id = 209,
                    Reference = "BK-CONC-2",
                    ServiceId = 1,
                    ServiceAddress = new ServiceAddress
                    {
                        Line1 = "19 Concurrency Avenue",
                        City = "Nairobi",
                        Country = "Kenya"
                    },
                    ScheduledStart = scheduledStart,
                    ScheduledEnd = scheduledEnd,
                    Status = BookingStatus.Confirmed,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            await db.SaveChangesAsync();
        }

        var managerA = CreateAuthenticatedClient(factory, "manager");
        var managerB = CreateAuthenticatedClient(factory, "manager");

        var firstAssignment = managerA.PostAsJsonAsync("/api/bookings/208/assign", new AssignHouseHelpRequest { HouseHelpId = 93 });
        var secondAssignment = managerB.PostAsJsonAsync("/api/bookings/209/assign", new AssignHouseHelpRequest { HouseHelpId = 93 });

        var responses = await Task.WhenAll(firstAssignment, secondAssignment);

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest));

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var bookings = await db.Bookings
                .Where(item => item.Id == 208 || item.Id == 209)
                .ToListAsync();

            Assert.Single(bookings.Where(item => item.Status == BookingStatus.Assigned));
            Assert.Equal(93, bookings.Single(item => item.Status == BookingStatus.Assigned).AssignedHouseHelpId);
        }
    }

    [Fact]
    public async Task CompleteRoute_TransitionsInProgressBookingToCompleted()
    {
        var factory = CreateFactory();
        await SeedServiceAsync(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            db.Bookings.Add(new Booking
            {
                Id = 1,
                Reference = "BK-COMPLETE",
                ServiceId = 1,
                ServiceAddress = new ServiceAddress
                {
                    Line1 = "2 Main Street",
                    City = "Nairobi",
                    Country = "Kenya"
                },
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(2),
                ScheduledEnd = DateTimeOffset.UtcNow.AddDays(2).AddHours(2),
                Status = BookingStatus.InProgress,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var manager = CreateAuthenticatedClient(factory, "admin");
        var response = await manager.PostAsync("/api/bookings/1/complete", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>();
        Assert.Equal(BookingStatus.Completed, payload!.Data!.Status);
    }

    [Fact]
    public async Task HouseHelpCanListAssignedBookings_UsingOwnUserClaim()
    {
        var factory = CreateFactory();
        await SeedServiceAsync(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var houseHelp = new HouseHelp
            {
                Id = 10,
                UserId = 42,
                FirstName = "Jane",
                LastName = "Helper",
                Phone = "+254700000010",
                City = "Nairobi",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.HouseHelps.Add(houseHelp);
            db.Bookings.Add(new Booking
            {
                Id = 100,
                Reference = "BK-ASSIGNED",
                ServiceId = 1,
                AssignedHouseHelpId = houseHelp.Id,
                ServiceAddress = new ServiceAddress
                {
                    Line1 = "10 Assigned Road",
                    City = "Nairobi",
                    Country = "Kenya"
                },
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(3),
                ScheduledEnd = DateTimeOffset.UtcNow.AddDays(3).AddHours(2),
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var houseHelpClient = CreateAuthenticatedClient(factory, "househelp", 42);
        var response = await houseHelpClient.GetAsync("/api/bookings/assigned/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<List<BookingDto>>>();
        Assert.NotNull(payload);
        Assert.Contains(payload!.Data!, dto => dto.Reference == "BK-ASSIGNED");
    }

    [Fact]
    public async Task AuthenticatedClientCanListOwnBookings_UsingUserClaim()
    {
        var factory = CreateFactory();
        await SeedServiceAsync(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var client = new Client
            {
                Id = 20,
                UserId = 77,
                Name = "Client User",
                Phone = "+254700000077",
                Email = "client-user@example.com",
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Clients.Add(client);
            db.Bookings.Add(new Booking
            {
                Id = 101,
                Reference = "BK-MINE",
                ServiceId = 1,
                ClientId = client.Id,
                ServiceAddress = new ServiceAddress
                {
                    Line1 = "40 Mine Lane",
                    City = "Nairobi",
                    Country = "Kenya"
                },
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(4),
                ScheduledEnd = DateTimeOffset.UtcNow.AddDays(4).AddHours(2),
                Status = BookingStatus.Requested,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var clientUser = CreateAuthenticatedClient(factory, "manager", 77);
        var response = await clientUser.GetAsync("/api/bookings/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<List<BookingDto>>>();
        Assert.NotNull(payload);
        Assert.Contains(payload!.Data!, dto => dto.Reference == "BK-MINE");
    }

    [Fact]
    public async Task AnonymousClientCanTrackBooking_ByReference()
    {
        var factory = CreateFactory();
        await SeedServiceAsync(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            db.Bookings.Add(new Booking
            {
                Id = 102,
                Reference = "BK-TRACK",
                ServiceId = 1,
                ServiceAddress = new ServiceAddress
                {
                    Line1 = "5 Tracking Road",
                    City = "Nairobi",
                    Country = "Kenya"
                },
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(5),
                ScheduledEnd = DateTimeOffset.UtcNow.AddDays(5).AddHours(2),
                Status = BookingStatus.InProgress,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync("/api/bookings/track/BK-TRACK");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<BookingTrackingDto>>();
        Assert.NotNull(payload);
        Assert.Equal("BK-TRACK", payload!.Data!.Reference);
        Assert.Equal(BookingStatus.InProgress, payload.Data.Status);
    }

    [Fact]
    public async Task GetList_FiltersByHouseHelpIdAndClientId_ForManagerOrAdmin()
    {
        var factory = CreateFactory();
        await SeedServiceAsync(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var houseHelp = new HouseHelp
            {
                Id = 200,
                FirstName = "Filter",
                LastName = "Helper",
                Phone = "+254700000200",
                City = "Nairobi",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var client = new Client
            {
                Id = 200,
                Name = "Filter Client",
                Phone = "+254700000201",
                Email = "filter-client@example.com",
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.HouseHelps.Add(houseHelp);
            db.Clients.Add(client);
            db.Bookings.AddRange(
                new Booking
                {
                    Id = 200,
                    Reference = "BK-FILTER-HH",
                    ServiceId = 1,
                    AssignedHouseHelpId = houseHelp.Id,
                    ServiceAddress = new ServiceAddress { Line1 = "1 Filter Rd", City = "Nairobi", Country = "Kenya" },
                    ScheduledStart = DateTimeOffset.UtcNow.AddDays(6),
                    ScheduledEnd = DateTimeOffset.UtcNow.AddDays(6).AddHours(2),
                    Status = BookingStatus.Assigned,
                    CreatedAt = DateTimeOffset.UtcNow
                },
                new Booking
                {
                    Id = 201,
                    Reference = "BK-FILTER-CLIENT",
                    ServiceId = 1,
                    ClientId = client.Id,
                    ServiceAddress = new ServiceAddress { Line1 = "2 Filter Rd", City = "Nairobi", Country = "Kenya" },
                    ScheduledStart = DateTimeOffset.UtcNow.AddDays(7),
                    ScheduledEnd = DateTimeOffset.UtcNow.AddDays(7).AddHours(2),
                    Status = BookingStatus.Requested,
                    CreatedAt = DateTimeOffset.UtcNow
                },
                new Booking
                {
                    Id = 202,
                    Reference = "BK-FILTER-OTHER",
                    ServiceId = 1,
                    ServiceAddress = new ServiceAddress { Line1 = "3 Filter Rd", City = "Nairobi", Country = "Kenya" },
                    ScheduledStart = DateTimeOffset.UtcNow.AddDays(8),
                    ScheduledEnd = DateTimeOffset.UtcNow.AddDays(8).AddHours(2),
                    Status = BookingStatus.Requested,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            await db.SaveChangesAsync();
        }

        var admin = CreateAuthenticatedClient(factory, "admin");

        var byHouseHelp = await admin.GetFromJsonAsync<ApiResponse<List<BookingDto>>>("/api/bookings?houseHelpId=200");
        Assert.NotNull(byHouseHelp);
        Assert.Single(byHouseHelp!.Data!);
        Assert.Equal("BK-FILTER-HH", byHouseHelp.Data![0].Reference);

        var byClient = await admin.GetFromJsonAsync<ApiResponse<List<BookingDto>>>("/api/bookings?clientId=200");
        Assert.NotNull(byClient);
        Assert.Single(byClient!.Data!);
        Assert.Equal("BK-FILTER-CLIENT", byClient.Data![0].Reference);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var dbName = $"booking_integration_{Guid.NewGuid():N}";
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<HouseContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<HouseContext>(options =>
                {
                    options.UseInMemoryDatabase(dbName);
                });
            });
        });
    }

    private static async Task SeedServiceAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HouseContext>();

        if (!await db.Services.AnyAsync())
        {
            db.Services.Add(new Service
            {
                Id = 1,
                Code = "BOOKING_TEST",
                Name = "Booking Test Service",
                Description = "Service for booking integration tests",
                BasePrice = 25m,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory, string role, int? userId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(role, userId));
        return client;
    }

    private static string CreateToken(string role, int? userId = null)
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.Role, role) };
        if (userId.HasValue)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, userId.Value.ToString()));
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        }

        var keyBytes = Encoding.UTF8.GetBytes(TestJwtKey);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "HouseManagement",
            audience: "HouseManagement",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
