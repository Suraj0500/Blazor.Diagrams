﻿using Blazor.Diagrams.Core.Extensions;
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Extensions;
using Blazor.Diagrams.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Blazor.Diagrams.Components.Renderers
{
    public class NodeRenderer : ComponentBase, IDisposable
    {
        private bool _shouldRender;
        private bool _isVisible = true;
        private bool _becameVisible;
        private ElementReference _element;
        private DotNetObjectReference<NodeRenderer>? _reference;
        private bool _isSvg;

        [CascadingParameter]
        public Diagram Diagram { get; set; } = null!;

        [Parameter]
        public NodeModel Node { get; set; } = null!;

        [Inject]
        private IJSRuntime JsRuntime { get; set; } = null!;

        public void Dispose()
        {
            Diagram.PanChanged -= CheckVisibility;
            Diagram.ZoomChanged -= CheckVisibility;
            Diagram.ContainerChanged -= CheckVisibility;
            Node.Changed -= ReRender;

            if (_element.Id != null)
                _ = JsRuntime.UnobserveResizes(_element);

            _reference?.Dispose();
        }

        [JSInvokable]
        public void OnResize(Size size)
        {
            // When the node becomes invisible (a.k.a unrendered), the size is zero
            if (Size.Zero.Equals(size))
                return;

            size = new Size(size.Width / Diagram.Zoom, size.Height / Diagram.Zoom);
            if (Node.Size != null && Node.Size.Width.AlmostEqualTo(size.Width) && Node.Size.Height.AlmostEqualTo(size.Height))
                return;

            Node.Size = size;
            Node.Refresh();
            Node.RefreshLinks();
            Node.ReinitializePorts();
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            _reference = DotNetObjectReference.Create(this);
            Diagram.PanChanged += CheckVisibility;
            Diagram.ZoomChanged += CheckVisibility;
            Diagram.ContainerChanged += CheckVisibility;
            Node.Changed += ReRender;
        }

        protected override void OnParametersSet()
        {
            base.OnParametersSet();

            _isSvg = Node is SvgNodeModel;
        }

        protected override bool ShouldRender()
        {
            if (!_shouldRender) return false;
            
            _shouldRender = false;
            return true;
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (!_isVisible)
                return;

            var componentType = Diagram.GetComponentForModel(Node) ?? (_isSvg ? typeof(SvgNodeWidget) : typeof(NodeWidget));
            var classes = new StringBuilder("node")
                .AppendIf(" locked", Node.Locked)
                .AppendIf(" selected", Node.Selected)
                .AppendIf(" grouped", Node.Group != null);

            builder.OpenElement(0, _isSvg ? "g" : "div");
            builder.AddAttribute(1, "class", classes.ToString());
            builder.AddAttribute(2, "data-node-id", Node.Id);

            if (_isSvg)
            {
                builder.AddAttribute(3, "transform", $"translate({Node.Position.X.ToInvariantString()} {Node.Position.Y.ToInvariantString()})");
            }
            else
            {
                builder.AddAttribute(3, "style", $"top: {Node.Position.Y.ToInvariantString()}px; left: {Node.Position.X.ToInvariantString()}px");
            }

            builder.AddAttribute(4, "onpointerdown", EventCallback.Factory.Create<PointerEventArgs>(this, OnPointerDown));
            builder.AddEventStopPropagationAttribute(5, "onpointerdown", true);
            builder.AddAttribute(6, "onpointerup", EventCallback.Factory.Create<PointerEventArgs>(this, OnPointerUp));
            builder.AddEventStopPropagationAttribute(7, "onpointerup", true);
            builder.AddAttribute(8, "onmouseenter", EventCallback.Factory.Create<MouseEventArgs>(this, OnMouseEnter));
            builder.AddAttribute(9, "onmouseleave", EventCallback.Factory.Create<MouseEventArgs>(this, OnMouseLeave));
            builder.AddElementReferenceCapture(10, value => _element = value);
            builder.OpenComponent(11, componentType);
            builder.AddAttribute(12, "Node", Node);
            builder.CloseComponent();
            builder.CloseElement();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (firstRender || _becameVisible)
            {
                _becameVisible = false;
                await JsRuntime.ObserveResizes(_element, _reference);
            }
        }

        private void CheckVisibility()
        {
            // _isVisible must be true in case virtualization gets disabled and some nodes are hidden
            if (!Diagram.Options.EnableVirtualization && _isVisible)
                return;

            if (Node.Size == null)
                return;

            var left = Node.Position.X * Diagram.Zoom + Diagram.Pan.X;
            var top = Node.Position.Y * Diagram.Zoom + Diagram.Pan.Y;
            var right = left + Node.Size.Width * Diagram.Zoom;
            var bottom = top + Node.Size.Height * Diagram.Zoom;

            var isVisible = right > 0 && left < Diagram.Container.Width && bottom > 0 &&
                top < Diagram.Container.Height;

            if (_isVisible != isVisible)
            {
                _isVisible = isVisible;
                _becameVisible = isVisible;
                ReRender();
            }
        }

        private void ReRender()
        {
            _shouldRender = true;
            InvokeAsync(StateHasChanged);
        }

        private void OnPointerDown(PointerEventArgs e) => Diagram.TriggerPointerDown(Node, e.ToCore());

        private void OnPointerUp(PointerEventArgs e) => Diagram.TriggerPointerUp(Node, e.ToCore());

        private void OnMouseEnter(MouseEventArgs e)
        {
            Console.WriteLine("On mouse enter");
            Diagram.TriggerPointerLeave(Node, e.ToCore());
        }

        private void OnMouseLeave(MouseEventArgs e)
        {
            Console.WriteLine("On mouse leave");
            Diagram.TriggerPointerEnter(Node, e.ToCore());
        }
    }
}