using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using WinServiceManager.Models;
using WinServiceManager.Services;
using WinServiceManager.Tests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace WinServiceManager.Tests.IntegrationTests.Dependencies
{
    /// <summary>
    /// Service dependency integration tests
    /// Tests service dependency creation, validation, and management
    /// </summary>
    [Collection("Integration Tests")]
    public class ServiceDependencyIntegrationTests : IClassFixture<ServiceTestFixture>, IDisposable
    {
        private readonly ServiceTestFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly ServiceManagerService _serviceManager;
        private readonly ServiceDependencyValidator? _validator;

        public ServiceDependencyIntegrationTests(ServiceTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _output = output ?? throw new ArgumentNullException(nameof(output));

            _serviceManager = new ServiceManagerService(null!, _fixture.MockDataStorage);
            var logger = new LoggerFactory().CreateLogger<ServiceDependencyValidator>();
            _validator = new ServiceDependencyValidator(_serviceManager, logger);
        }

        public void Dispose()
        {
            // Cleanup handled by test fixture
        }

        [Fact]
        public async Task CreateService_WithSingleDependency_CreatesSuccessfully()
        {
            // Arrange
            var dependency = _fixture.CreateTestService("SingleDepTest_Dependency");
            var dependent = _fixture.CreateTestService("SingleDepTest_Dependent");
            dependent.Dependencies.Add(dependency.Id);

            // Add dependency to storage
            await _fixture.MockDataStorage.AddServiceAsync(dependency);

            // Act
            var result = await _validator!.ValidateDependenciesAsync(dependent);

            // Assert
            result.IsValid.Should().BeTrue("validation should succeed with valid dependency");
            result.Errors.Should().BeEmpty("should have no validation errors");
            result.StartupOrder.Should().Contain(dependency.Id);
            result.StartupOrder.Should().Contain(dependent.Id);
            result.StartupOrder.Should().HaveCount(2, "should have both services in startup order");

            // Dependency should come before dependent service
            var depIndex = result.StartupOrder.IndexOf(dependency.Id);
            var dependentIndex = result.StartupOrder.IndexOf(dependent.Id);
            depIndex.Should().BeLessThan(dependentIndex, "dependency should start before dependent service");

            _output.WriteLine($"Startup order: {string.Join(" -> ", result.StartupOrder)}");
        }

        [Fact]
        public async Task CreateService_WithMultipleDependencies_CreatesSuccessfully()
        {
            // Arrange
            var dep1 = _fixture.CreateTestService("MultiDepTest_Dep1");
            var dep2 = _fixture.CreateTestService("MultiDepTest_Dep2");
            var dep3 = _fixture.CreateTestService("MultiDepTest_Dep3");
            var dependent = _fixture.CreateTestService("MultiDepTest_Dependent");

            dependent.Dependencies.Add(dep1.Id);
            dependent.Dependencies.Add(dep2.Id);
            dependent.Dependencies.Add(dep3.Id);

            // Add dependencies to storage
            await _fixture.MockDataStorage.AddServiceAsync(dep1);
            await _fixture.MockDataStorage.AddServiceAsync(dep2);
            await _fixture.MockDataStorage.AddServiceAsync(dep3);

            // Act
            var result = await _validator!.ValidateDependenciesAsync(dependent);

            // Assert
            result.IsValid.Should().BeTrue("validation should succeed with multiple valid dependencies");
            result.Errors.Should().BeEmpty();

            // All dependencies should be in startup order before the dependent service
            var dep1Index = result.StartupOrder.IndexOf(dep1.Id);
            var dep2Index = result.StartupOrder.IndexOf(dep2.Id);
            var dep3Index = result.StartupOrder.IndexOf(dep3.Id);
            var dependentIndex = result.StartupOrder.IndexOf(dependent.Id);

            dep1Index.Should().BeGreaterOrEqualTo(0, "dep1 should be in startup order");
            dep2Index.Should().BeGreaterOrEqualTo(0, "dep2 should be in startup order");
            dep3Index.Should().BeGreaterOrEqualTo(0, "dep3 should be in startup order");

            dependentIndex.Should().BeGreaterThan(dep1Index, "dependent should start after dep1");
            dependentIndex.Should().BeGreaterThan(dep2Index, "dependent should start after dep2");
            dependentIndex.Should().BeGreaterThan(dep3Index, "dependent should start after dep3");

            _output.WriteLine($"Startup order: {string.Join(" -> ", result.StartupOrder)}");
        }

        [Fact]
        public async Task ValidateCircularDependency_WithDirectCycle_IsDetected()
        {
            // Arrange - Direct circular dependency: A depends on B, B depends on A
            var serviceA = _fixture.CreateTestService("CycleTest_A");
            var serviceB = _fixture.CreateTestService("CycleTest_B");

            serviceA.Dependencies.Add(serviceB.Id);
            serviceB.Dependencies.Add(serviceA.Id);

            await _fixture.MockDataStorage.AddServiceAsync(serviceA);
            await _fixture.MockDataStorage.AddServiceAsync(serviceB);

            // Act
            var result = await _validator!.ValidateDependenciesAsync(serviceA);

            // Assert
            result.IsValid.Should().BeFalse("circular dependency should be detected");
            result.Errors.Should().NotBeEmpty("should have validation errors");
            result.Errors.Should().Contain(e => e.Contains("循环依赖") || e.Contains("circular"));

            _output.WriteLine($"Circular dependency detected: {result.GetErrorSummary()}");
        }

        [Fact]
        public async Task ValidateCircularDependency_WithIndirectCycle_IsDetected()
        {
            // Arrange - Indirect circular dependency: A -> B -> C -> A
            var serviceA = _fixture.CreateTestService("IndirectCycle_A");
            var serviceB = _fixture.CreateTestService("IndirectCycle_B");
            var serviceC = _fixture.CreateTestService("IndirectCycle_C");

            serviceA.Dependencies.Add(serviceB.Id);
            serviceB.Dependencies.Add(serviceC.Id);
            serviceC.Dependencies.Add(serviceA.Id);

            await _fixture.MockDataStorage.AddServiceAsync(serviceA);
            await _fixture.MockDataStorage.AddServiceAsync(serviceB);
            await _fixture.MockDataStorage.AddServiceAsync(serviceC);

            // Act
            var result = await _validator!.ValidateDependenciesAsync(serviceA);

            // Assert
            result.IsValid.Should().BeFalse("indirect circular dependency should be detected");
            result.Errors.Should().NotBeEmpty();
            result.Errors.Should().Contain(e => e.Contains("循环依赖") || e.Contains("circular"));

            _output.WriteLine($"Indirect circular dependency detected: {result.GetErrorSummary()}");
        }

        [Fact]
        public async Task ValidateCircularDependency_WithSelfDependency_IsDetected()
        {
            // Arrange - Service depends on itself
            var service = _fixture.CreateTestService("SelfCycleTest");
            service.Dependencies.Add(service.Id);

            await _fixture.MockDataStorage.AddServiceAsync(service);

            // Act
            var result = await _validator!.ValidateDependenciesAsync(service);

            // Assert
            result.IsValid.Should().BeFalse("self-dependency should be detected");
            result.Errors.Should().NotBeEmpty();

            _output.WriteLine($"Self-dependency detected: {result.GetErrorSummary()}");
        }

        [Fact]
        public async Task CalculateStartupOrder_WithDependencies_ReturnsCorrectOrder()
        {
            // Arrange - Complex dependency chain: D -> C -> B -> A
            var serviceA = _fixture.CreateTestService("OrderTest_A");
            var serviceB = _fixture.CreateTestService("OrderTest_B");
            var serviceC = _fixture.CreateTestService("OrderTest_C");
            var serviceD = _fixture.CreateTestService("OrderTest_D");

            serviceB.Dependencies.Add(serviceA.Id);
            serviceC.Dependencies.Add(serviceB.Id);
            serviceD.Dependencies.Add(serviceC.Id);

            await _fixture.MockDataStorage.AddServiceAsync(serviceA);
            await _fixture.MockDataStorage.AddServiceAsync(serviceB);
            await _fixture.MockDataStorage.AddServiceAsync(serviceC);
            await _fixture.MockDataStorage.AddServiceAsync(serviceD);

            // Act - Get startup order for the top-level service (D)
            var result = await _validator!.ValidateDependenciesAsync(serviceD);

            // Assert
            result.IsValid.Should().BeTrue();
            result.StartupOrder.Should().HaveCount(4, "all services should be in startup order");

            // Verify order: A -> B -> C -> D
            var order = result.StartupOrder;
            order[0].Should().Be(serviceA.Id, "A should start first");
            order[1].Should().Be(serviceB.Id, "B should start second");
            order[2].Should().Be(serviceC.Id, "C should start third");
            order[3].Should().Be(serviceD.Id, "D should start last");

            _output.WriteLine($"Startup order: {string.Join(" -> ", order)}");
        }

        [Fact]
        public async Task CalculateStartupOrder_WithDiamondDependency_HandlesCorrectly()
        {
            // Arrange - Diamond dependency: Both B and C depend on A, D depends on both B and C
            //     A
            //    / \
            //   B   C
            //    \ /
            //     D
            var serviceA = _fixture.CreateTestService("DiamondTest_A");
            var serviceB = _fixture.CreateTestService("DiamondTest_B");
            var serviceC = _fixture.CreateTestService("DiamondTest_C");
            var serviceD = _fixture.CreateTestService("DiamondTest_D");

            serviceB.Dependencies.Add(serviceA.Id);
            serviceC.Dependencies.Add(serviceA.Id);
            serviceD.Dependencies.Add(serviceB.Id);
            serviceD.Dependencies.Add(serviceC.Id);

            await _fixture.MockDataStorage.AddServiceAsync(serviceA);
            await _fixture.MockDataStorage.AddServiceAsync(serviceB);
            await _fixture.MockDataStorage.AddServiceAsync(serviceC);
            await _fixture.MockDataStorage.AddServiceAsync(serviceD);

            // Act
            var result = await _validator!.ValidateDependenciesAsync(serviceD);

            // Assert
            result.IsValid.Should().BeTrue("diamond dependency should be valid");

            // A should come before both B and C
            var aIndex = result.StartupOrder.IndexOf(serviceA.Id);
            var bIndex = result.StartupOrder.IndexOf(serviceB.Id);
            var cIndex = result.StartupOrder.IndexOf(serviceC.Id);
            var dIndex = result.StartupOrder.IndexOf(serviceD.Id);

            aIndex.Should().BeLessThan(bIndex, "A should start before B");
            aIndex.Should().BeLessThan(cIndex, "A should start before C");
            bIndex.Should().BeLessThan(dIndex, "B should start before D");
            cIndex.Should().BeLessThan(dIndex, "C should start before D");

            _output.WriteLine($"Diamond dependency startup order: {string.Join(" -> ", result.StartupOrder)}");
        }

        [Fact]
        public async Task ValidateDependency_WithNonExistentDependency_FailsValidation()
        {
            // Arrange
            var service = _fixture.CreateTestService("MissingDepTest");
            service.Dependencies.Add("NonExistentServiceId");

            await _fixture.MockDataStorage.AddServiceAsync(service);

            // Act
            var result = await _validator!.ValidateDependenciesAsync(service);

            // Assert
            result.IsValid.Should().BeFalse("non-existent dependency should fail validation");
            result.Errors.Should().NotBeEmpty();
            result.Errors.Should().Contain(e => e.Contains("NonExistentServiceId") || e.Contains("不存在"));

            _output.WriteLine($"Non-existent dependency detected: {result.GetErrorSummary()}");
        }

        [Fact]
        public async Task ValidateDependency_WithMultipleNonExistentDependencies_FailsValidation()
        {
            // Arrange
            var service = _fixture.CreateTestService("MultipleMissingDepTest");
            service.Dependencies.Add("NonExistent1");
            service.Dependencies.Add("NonExistent2");
            service.Dependencies.Add("NonExistent3");

            await _fixture.MockDataStorage.AddServiceAsync(service);

            // Act
            var result = await _validator!.ValidateDependenciesAsync(service);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().HaveCountGreaterOrEqualTo(3, "should have errors for each missing dependency");

            _output.WriteLine($"Multiple missing dependencies detected: {result.GetErrorSummary()}");
        }

        [Fact]
        public async Task ValidateDependency_WithMixedValidAndInvalidDependencies_PartiallyFails()
        {
            // Arrange
            var validDep = _fixture.CreateTestService("MixedTest_Valid");
            var service = _fixture.CreateTestService("MixedTest_Service");

            service.Dependencies.Add(validDep.Id);      // Valid
            service.Dependencies.Add("InvalidServiceId"); // Invalid

            await _fixture.MockDataStorage.AddServiceAsync(validDep);
            await _fixture.MockDataStorage.AddServiceAsync(service);

            // Act
            var result = await _validator!.ValidateDependenciesAsync(service);

            // Assert
            result.IsValid.Should().BeFalse("any invalid dependency should cause failure");
            result.Errors.Should().NotBeEmpty();
            result.Errors.Should().Contain(e => e.Contains("InvalidServiceId") || e.Contains("不存在"));

            _output.WriteLine($"Partial validation failed: {result.GetErrorSummary()}");
        }

        [Fact]
        public async Task ValidateDependency_WithNoDependencies_SucceedsWithSingleService()
        {
            // Arrange
            var service = _fixture.CreateTestService("NoDepTest");

            await _fixture.MockDataStorage.AddServiceAsync(service);

            // Act
            var result = await _validator!.ValidateDependenciesAsync(service);

            // Assert
            result.IsValid.Should().BeTrue("service without dependencies should be valid");
            result.Errors.Should().BeEmpty();
            result.StartupOrder.Should().ContainSingle("should have only the service itself");
            result.StartupOrder[0].Should().Be(service.Id);

            _output.WriteLine($"No dependency validation passed. Startup order: {string.Join(" -> ", result.StartupOrder)}");
        }

        [Fact]
        public async Task ValidateMultipleServices_EachWithDependencies_CalculatesCorrectOrders()
        {
            // Arrange - Multiple independent dependency chains
            // Chain 1: A1 -> B1
            // Chain 2: A2 -> B2 -> C2
            var a1 = _fixture.CreateTestService("MultiChainTest_A1");
            var b1 = _fixture.CreateTestService("MultiChainTest_B1");

            var a2 = _fixture.CreateTestService("MultiChainTest_A2");
            var b2 = _fixture.CreateTestService("MultiChainTest_B2");
            var c2 = _fixture.CreateTestService("MultiChainTest_C2");

            b1.Dependencies.Add(a1.Id);
            b2.Dependencies.Add(a2.Id);
            c2.Dependencies.Add(b2.Id);

            await _fixture.MockDataStorage.AddServiceAsync(a1);
            await _fixture.MockDataStorage.AddServiceAsync(b1);
            await _fixture.MockDataStorage.AddServiceAsync(a2);
            await _fixture.MockDataStorage.AddServiceAsync(b2);
            await _fixture.MockDataStorage.AddServiceAsync(c2);

            // Act - Validate each chain
            var result1 = await _validator!.ValidateDependenciesAsync(b1);
            var result2 = await _validator!.ValidateDependenciesAsync(c2);

            // Assert
            result1.IsValid.Should().BeTrue();
            result1.StartupOrder.Should().HaveCount(2);
            result1.StartupOrder[0].Should().Be(a1.Id);
            result1.StartupOrder[1].Should().Be(b1.Id);

            result2.IsValid.Should().BeTrue();
            result2.StartupOrder.Should().HaveCount(3);
            result2.StartupOrder[0].Should().Be(a2.Id);
            result2.StartupOrder[1].Should().Be(b2.Id);
            result2.StartupOrder[2].Should().Be(c2.Id);

            _output.WriteLine($"Chain 1 order: {string.Join(" -> ", result1.StartupOrder)}");
            _output.WriteLine($"Chain 2 order: {string.Join(" -> ", result2.StartupOrder)}");
        }
    }
}
