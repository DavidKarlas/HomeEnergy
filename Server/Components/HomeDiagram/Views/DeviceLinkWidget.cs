using Blazor.Diagrams.Components;
using Blazor.Diagrams.Core.Anchors;
using Blazor.Diagrams.Core.Models;
using Microsoft.AspNetCore.Components.Rendering;
using PropertyChanged.SourceGenerator;
using System.ComponentModel;

namespace HomeEnergy.Components.HomeDiagram.Views
{
    public partial class DeviceLinkWidget : LinkWidget
    {
        protected override void BuildRenderTree(RenderTreeBuilder __builder)
        {
            __builder.OpenElement(0, "g");
            __builder.AddAttribute(0, "class", ((DeviceLinkModel)Link).CssClasses);
            base.BuildRenderTree(__builder);
            __builder.CloseElement();
        }
    }
}
