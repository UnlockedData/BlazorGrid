﻿using BlazorGrid.Abstractions;
using BlazorGrid.Config;
using BlazorGrid.Config.Styles;
using BlazorGrid.Demo.Pages.Examples;
using BlazorGrid.Demo.Tests.Mock;
using BlazorGrid.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace BlazorGrid.Tests.Demo
{
    [TestClass]
    public class PreLoadedPageTests : Bunit.TestContext
    {
        [TestMethod]
        public void Can_Render_Page()
        {
            var provider = new Mock<IGridProvider>();
            Services.AddSingleton(provider.Object);

            var nav = new MockNav();
            Services.AddSingleton<NavigationManager>(nav);

            Services.AddSingleton<IBlazorGridConfig>(_ => new DefaultConfig { Styles = new SpectreStyles() });

            RenderComponent<PreLoaded>();
        }
    }
}
