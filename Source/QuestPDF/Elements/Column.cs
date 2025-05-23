﻿using System;
using System.Collections.Generic;
using System.Linq;
using QuestPDF.Drawing;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QuestPDF.Elements
{
    internal sealed class ColumnItemRenderingCommand
    {
        public Element Element { get; set; }
        public SpacePlan Measurement { get; set; }
        public Position Offset { get; set; }
    }

    internal sealed class Column : Element, IStateful
    {
        internal List<Element> Items { get; } = new();
        internal float Spacing { get; set; }
        
        internal override IEnumerable<Element?> GetChildren()
        {
            return Items;
        }
        
        internal override void CreateProxy(Func<Element?, Element?> create)
        {
            for (var i = 0; i < Items.Count; i++)
                Items[i] = create(Items[i]);
        }

        internal override SpacePlan Measure(Size availableSpace)
        {
            if (!Items.Any())
                return SpacePlan.Empty();
            
            if (CurrentRenderingIndex == Items.Count)
                return SpacePlan.Empty();
            
            if (availableSpace.IsNegative())
                return SpacePlan.Wrap("The available space is negative.");
            
            var renderingCommands = PlanLayout(availableSpace);

            if (!renderingCommands.Any())
                return SpacePlan.Wrap("The available space is not sufficient for even partially rendering a single item.");

            var width = renderingCommands.Max(x => x.Measurement.Width);
            var height = renderingCommands.Last().Offset.Y + renderingCommands.Last().Measurement.Height;
            var size = new Size(width, height);
            
            if (width > availableSpace.Width + Size.Epsilon)
                return SpacePlan.Wrap("The content requires more horizontal space than available.");
            
            if (height > availableSpace.Height + Size.Epsilon)
                return SpacePlan.Wrap("The content requires more vertical space than available.");
            
            var totalRenderedItems = CurrentRenderingIndex + renderingCommands.Count(x => x.Measurement.Type is SpacePlanType.Empty or SpacePlanType.FullRender);
            var willBeFullyRendered = totalRenderedItems == Items.Count;

            return willBeFullyRendered
                ? SpacePlan.FullRender(size)
                : SpacePlan.PartialRender(size);
        }

        internal override void Draw(Size availableSpace)
        {
            var renderingCommands = PlanLayout(availableSpace);

            foreach (var command in renderingCommands)
            {
                var targetSize = new Size(availableSpace.Width, command.Measurement.Height);

                Canvas.Translate(command.Offset);
                command.Element.Draw(targetSize);
                Canvas.Translate(command.Offset.Reverse());
            }
            
            var fullyRenderedItems = renderingCommands.Count(x => x.Measurement.Type is SpacePlanType.Empty or SpacePlanType.FullRender);
            CurrentRenderingIndex += fullyRenderedItems;
        }

        private List<ColumnItemRenderingCommand> PlanLayout(Size availableSpace)
        {
            var topOffset = 0f;
            var commands = new List<ColumnItemRenderingCommand>();

            foreach (var item in Items.Skip(CurrentRenderingIndex))
            {
                var isFirstItem = commands.Count == 0;

                var availableHeight = availableSpace.Height - topOffset;
                
                if (availableHeight < -Size.Epsilon)
                    break;

                availableHeight = Math.Max(0, availableHeight);
                
                if (!isFirstItem)
                    availableHeight -= Spacing;

                var allowOnlyZeroSpaceItems = availableHeight < Size.Epsilon;
                
                var itemSpace = allowOnlyZeroSpaceItems
                    ? Size.Zero
                    : new Size(availableSpace.Width, availableHeight);
                
                var measurement = item.Measure(itemSpace);
                
                if (measurement.Type == SpacePlanType.Wrap)
                    break;

                var currentItemTookSpace = !Size.Equal(measurement, Size.Zero);
                
                if (allowOnlyZeroSpaceItems && currentItemTookSpace)
                    break;

                if (!isFirstItem && currentItemTookSpace)
                    topOffset += Spacing;
                
                commands.Add(new ColumnItemRenderingCommand
                {
                    Element = item,
                    Measurement = measurement,
                    Offset = new Position(0, topOffset)
                });

                if (measurement.Type == SpacePlanType.PartialRender)
                    break;

                topOffset += measurement.Height;
            }

            return commands;
        }
        
        #region IStateful
        
        internal int CurrentRenderingIndex { get; set; }
    
        public void ResetState(bool hardReset = false) => CurrentRenderingIndex = 0;
        public object GetState() => CurrentRenderingIndex;
        public void SetState(object state) => CurrentRenderingIndex = (int) state;
        
        #endregion
    }
}