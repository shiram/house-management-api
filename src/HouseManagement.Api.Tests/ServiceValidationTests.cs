using System.ComponentModel.DataAnnotations;
using HouseManagement.Api.DTOs;
using Xunit;

namespace HouseManagement.Api.Tests;

public class ServiceValidationTests
{
    [Fact]
    public void CreateServiceRequest_RejectsInvalidCodeAndNegativePrice()
    {
        var request = new CreateServiceRequest
        {
            Code = "invalid code",
            Name = "Cleaning",
            BasePrice = -1
        };

        var errors = Validate(request);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(CreateServiceRequest.Code)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(CreateServiceRequest.BasePrice)));
    }

    [Fact]
    public void CreateServiceRequest_AcceptsValidValues()
    {
        var request = new CreateServiceRequest
        {
            Code = "HOUSE_CLEANING",
            Name = "House Cleaning",
            Description = "Standard cleaning service",
            BasePrice = 25.50m
        };

        Assert.Empty(Validate(request));
    }

    private static IList<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }
}
