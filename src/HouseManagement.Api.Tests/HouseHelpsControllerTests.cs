using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using HouseManagement.Api.Controllers;
using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Services;
using HouseManagement.Api.Models;
using HouseManagement.Api.DTOs;

namespace HouseManagement.Api.Tests;

public class HouseHelpsControllerTests
{
    [Fact]
    public async Task GetAll_ReturnsOk_WithDtos()
    {
        var mockSvc = new Mock<IHouseHelpService>();
        mockSvc.Setup(s => s.GetFilteredAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new List<HouseHelp>
        {
            new HouseHelp { Id = 1, FirstName = "A", LastName = "B", Phone = "+1", City = "C", IsActive = true }
        });

        var controller = new HouseHelpsController(mockSvc.Object);

        var res = await controller.GetAll(null, null, null, null, null);
        var ok = Assert.IsType<OkObjectResult>(res);
        var envelope = Assert.IsType<ApiResponse<IEnumerable<HouseHelpDto>>>(ok.Value);
        Assert.Equal(200, envelope.StatusCode);
        var items = Assert.IsAssignableFrom<IEnumerable<HouseHelpDto>>(envelope.Data);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenMissing()
    {
        var mockSvc = new Mock<IHouseHelpService>();
        mockSvc.Setup(s => s.GetByIdAsync(1)).ReturnsAsync((HouseHelp?)null);
        var controller = new HouseHelpsController(mockSvc.Object);

        var res = await controller.Get(1);
        Assert.IsType<NotFoundResult>(res);
    }

    [Fact]
    public async Task Get_ReturnsEnvelope_OnSuccess()
    {
        var mockSvc = new Mock<IHouseHelpService>();
        mockSvc.Setup(s => s.GetByIdAsync(5)).ReturnsAsync(new HouseHelp
        {
            Id = 5,
            FirstName = "A",
            LastName = "B",
            Phone = "+1",
            City = "C",
            IsActive = true
        });

        var controller = new HouseHelpsController(mockSvc.Object);

        var res = await controller.Get(5);
        var ok = Assert.IsType<OkObjectResult>(res);
        var envelope = Assert.IsType<ApiResponse<HouseHelpDto>>(ok.Value);
        Assert.Equal(200, envelope.StatusCode);
        Assert.Equal(5, envelope.Data!.Id);
    }

    [Fact]
    public async Task Create_ReturnsCreated_OnSuccess()
    {
        var mockSvc = new Mock<IHouseHelpService>();
        var hh = new HouseHelp { Id = 5, FirstName = "X", LastName = "Y", Phone = "+1", City = "Z" };
        mockSvc.Setup(s => s.CreateAsync(It.IsAny<HouseHelp>(), It.IsAny<IEnumerable<string>?>())).ReturnsAsync(hh);
        var controller = new HouseHelpsController(mockSvc.Object);

        var req = new CreateHouseHelpRequest { FirstName = "X", LastName = "Y", Phone = "+1", City = "Z" };
        var res = await controller.Create(req);
        var created = Assert.IsType<CreatedAtActionResult>(res);
        var envelope = Assert.IsType<ApiResponse<HouseHelpDto>>(created.Value);
        Assert.Equal(201, envelope.StatusCode);
        Assert.Equal(5, envelope.Data!.Id);
    }

    [Fact]
    public async Task GetAll_UsesLocationAndSearchParameters()
    {
        var mockSvc = new Mock<IHouseHelpService>();
        mockSvc.Setup(s => s.GetFilteredAsync(null, null, null, null, null, "nairobi", "smith"))
            .ReturnsAsync(new List<HouseHelp>());

        var controller = new HouseHelpsController(mockSvc.Object);
        var res = await controller.GetAll(null, null, null, null, null, "nairobi", "smith");

        var ok = Assert.IsType<OkObjectResult>(res);
        var envelope = Assert.IsType<ApiResponse<IEnumerable<HouseHelpDto>>>(ok.Value);
        Assert.Equal(200, envelope.StatusCode);
    }
}