using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using HouseManagement.Api.Controllers;
using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Services;
using HouseManagement.Api.Models;
using HouseManagement.Api.DTOs;
using System.Collections.Generic;

namespace HouseManagement.Api.Tests;

public class HouseHelpsControllerValidationTests
{
    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelStateInvalid()
    {
        var mockSvc = new Mock<IHouseHelpService>();
        var controller = new HouseHelpsController(mockSvc.Object);
        controller.ModelState.AddModelError("Phone", "Invalid phone");

        var req = new CreateHouseHelpRequest { FirstName = "X", LastName = "Y", Phone = "bad", City = "Z" };
        var res = await controller.Create(req);

        var badRequest = Assert.IsType<BadRequestObjectResult>(res);
        var envelope = Assert.IsType<ApiResponse<Dictionary<string, string[]>>>(badRequest.Value);
        Assert.Equal(400, envelope.StatusCode);
        Assert.Equal("Validation failed", envelope.Message);
        Assert.Contains("Phone", envelope.Data.Keys);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenHouseHelpMissing()
    {
        var mockSvc = new Mock<IHouseHelpService>();
        mockSvc.Setup(s => s.GetByIdAsync(99)).ReturnsAsync((HouseHelp?)null);
        var controller = new HouseHelpsController(mockSvc.Object);

        var req = new UpdateHouseHelpRequest { FirstName = "X", LastName = "Y", Phone = "+1", City = "Z" };
        var res = await controller.Update(99, req);
        Assert.IsType<NotFoundResult>(res);
    }

    [Fact]
    public async Task GetAll_UsesFilterParameters()
    {
        var mockSvc = new Mock<IHouseHelpService>();
        mockSvc.Setup(s => s.GetFilteredAsync("Nairobi", "Cleaning", true, 1, 10, null))
            .ReturnsAsync(new List<HouseHelp> { new HouseHelp { Id = 1, FirstName = "A", LastName = "B", Phone = "+1", City = "Nairobi", IsActive = true } });

        var controller = new HouseHelpsController(mockSvc.Object);
        var res = await controller.GetAll("Nairobi", "Cleaning", 1, 10);
        var ok = Assert.IsType<OkObjectResult>(res);
        var envelope = Assert.IsType<ApiResponse<IEnumerable<PublicHouseHelpDto>>>(ok.Value);
        Assert.Equal(200, envelope.StatusCode);
        Assert.NotNull(envelope.Data);
    }
}