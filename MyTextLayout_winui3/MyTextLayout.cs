// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT License. See LICENSE.txt in the project root for license information.

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using Windows.Foundation;

namespace MyTextLayout_winui3
{
    public class MyTextLayout
    {
        List<Rect> layoutRectangles;
        List<LayoutBox> layoutBoxes;
        List<KeyValuePair<CanvasCharacterRange, CanvasSolidColorBrush>> foregroundColors = new List<KeyValuePair<CanvasCharacterRange, CanvasSolidColorBrush>>();
        bool needsLayout = true;
        string text;

        public bool ShouldJustify { get; set; }

        public CanvasTextFormat TextFormat
        {
            get;
            set;
        }

        public CanvasTextDirection TextDirection
        {
            get;
            set;
        }

        public float TabWidth
        {
            get;
            set;
        }

        Size _RequireSize;
        public Size RequireSize
        {
            get
            {
                return _RequireSize;
            }
            set
            {
                _RequireSize = value;
                needsLayout = true;
            }
        }

        Size? _ActualSize;
        public Size ActualSize
        {
            get
            {
                if (_ActualSize == null)
                    this.EnsureLayout();
                return _ActualSize.Value;
            }
        }

        public bool IsDrawControlCode
        {
            get;
            set;
        }

        public CanvasSolidColorBrush DefaultForegorundBrush
        {
            get;
            set;
        }

        public MyTextLayout(string t)
        {
            this.text = t;
            this.TabWidth = 48;
            this.TextDirection = CanvasTextDirection.LeftToRightThenTopToBottom;
        }

        struct FormattingSpan
        {
            public CanvasGlyph[] Glyphs;
            public CanvasFontFace FontFace;
            public float FontSize;
            public CanvasAnalyzedScript Script;
            public int[] ClusterMap;
            public CanvasGlyphShaping[] GlyphShaping;
            public CanvasCharacterRange Range;
            public bool NeedsAdditionalJustificationCharacters;
            public uint BidiLevel;
            public CanvasSolidColorBrush ForegroundColor;
        }

        class FormattingSpanHelper
        {
            int foregroundColorRunIndex = 0;
            int fontRunIndex = 0;
            int scriptRunIndex = 0;
            int bidiRunIndex = 0;

            CanvasCharacterRange characterRange;

            IReadOnlyList<KeyValuePair<CanvasCharacterRange, CanvasSolidColorBrush>> foregroundColorRuns;
            IReadOnlyList<KeyValuePair<CanvasCharacterRange, CanvasScaledFont>> fontRuns;
            IReadOnlyList<KeyValuePair<CanvasCharacterRange, CanvasAnalyzedScript>> scriptRuns;
            IReadOnlyList<KeyValuePair<CanvasCharacterRange, CanvasAnalyzedBidi>> bidiRuns;

            public FormattingSpanHelper(
                IReadOnlyList<KeyValuePair<CanvasCharacterRange, CanvasScaledFont>> f,
                IReadOnlyList<KeyValuePair<CanvasCharacterRange, CanvasAnalyzedScript>> s,
                IReadOnlyList<KeyValuePair<CanvasCharacterRange, CanvasAnalyzedBidi>> b,
                IReadOnlyList<KeyValuePair<CanvasCharacterRange, CanvasSolidColorBrush>> fc)
            {
                fontRuns = f;
                scriptRuns = s;
                bidiRuns = b;
                foregroundColorRuns = fc;

                characterRange.CharacterCount = Min(fontRuns[0].Key.CharacterCount, scriptRuns[0].Key.CharacterCount, bidiRuns[0].Key.CharacterCount, foregroundColorRuns[0].Key.CharacterCount);
            }

            public void MoveNext()
            {
                int forgroundColorRunBoundary = GetBoundary(foregroundColorRuns[foregroundColorRunIndex].Key);
                int fontRunBoundary = GetBoundary(fontRuns[fontRunIndex].Key);
                int scriptRunBoundary = GetBoundary(scriptRuns[scriptRunIndex].Key);
                int bidiRunBoundary = GetBoundary(bidiRuns[bidiRunIndex].Key);

                int soonestBoundary = characterRange.CharacterIndex + characterRange.CharacterCount;

                if (soonestBoundary == forgroundColorRunBoundary)
                {
                    foregroundColorRunIndex++;

                    if (foregroundColorRunIndex < foregroundColorRuns.Count)
                        forgroundColorRunBoundary = GetBoundary(foregroundColorRuns[foregroundColorRunIndex].Key);
                }

                if (soonestBoundary == fontRunBoundary)
                {
                    fontRunIndex++;

                    if (fontRunIndex < fontRuns.Count)
                        fontRunBoundary = GetBoundary(fontRuns[fontRunIndex].Key);
                }

                if (soonestBoundary == scriptRunBoundary)
                {
                    scriptRunIndex++;

                    if (scriptRunIndex < scriptRuns.Count)
                        scriptRunBoundary = GetBoundary(scriptRuns[scriptRunIndex].Key);
                }

                if (soonestBoundary == bidiRunBoundary)
                {
                    bidiRunIndex++;

                    if (bidiRunIndex < bidiRuns.Count)
                        bidiRunBoundary = GetBoundary(bidiRuns[bidiRunIndex].Key);
                }

                int nextBoundary = Min(fontRunBoundary, scriptRunBoundary, bidiRunBoundary, forgroundColorRunBoundary);

                characterRange.CharacterIndex += characterRange.CharacterCount;
                characterRange.CharacterCount = nextBoundary - characterRange.CharacterIndex;
            }

            public bool IsDone()
            {
                return !(fontRunIndex < fontRuns.Count &&
                    scriptRunIndex < scriptRuns.Count &&
                    bidiRunIndex < bidiRuns.Count &&
                    foregroundColorRunIndex < foregroundColorRuns.Count);
            }

            public CanvasScaledFont ScaledFont { get { return fontRuns[fontRunIndex].Value; } }
            public CanvasAnalyzedScript Script { get { return scriptRuns[scriptRunIndex].Value; } }
            public CanvasAnalyzedBidi Bidi { get { return bidiRuns[bidiRunIndex].Value; } }
            public CanvasSolidColorBrush Foreground { get { return foregroundColorRuns[foregroundColorRunIndex].Value;  } }

            public CanvasCharacterRange Range { get { return characterRange; } }


            int GetBoundary(CanvasCharacterRange range)
            {
                return range.CharacterIndex + range.CharacterCount;
            }

            int Min(params int[] a)
            {
                return a.Min();
            }

        }

        List<FormattingSpan> EvaluateFormattingSpans(
            string text,
            CanvasTextAnalyzer textAnalyzer,
            IReadOnlyList<KeyValuePair<CanvasCharacterRange, CanvasScaledFont>> fontRuns,
            IReadOnlyList<KeyValuePair<CanvasCharacterRange, CanvasAnalyzedScript>> scriptRuns,
            IReadOnlyList<KeyValuePair<CanvasCharacterRange, CanvasAnalyzedBidi>> bidiRuns,
            IReadOnlyList<KeyValuePair<CanvasCharacterRange, CanvasSolidColorBrush>> fcRuns,
            out float maxLineSpacing)
        {
            maxLineSpacing = 0;

            //
            // Divide up our text space into spans of uniform font face, script and bidi and color.
            //

            List<FormattingSpan> formattingSpans = new List<FormattingSpan>();

            FormattingSpanHelper formattingSpanHelper = new FormattingSpanHelper(fontRuns, scriptRuns, bidiRuns, fcRuns);

            float posx = 0;
            while (!formattingSpanHelper.IsDone())
            {
                var scriptProperties = textAnalyzer.GetScriptProperties(formattingSpanHelper.Script);

                float fontSize = TextFormat.FontSize * formattingSpanHelper.ScaledFont.ScaleFactor;

                FormattingSpan formattingSpan = new FormattingSpan();

                int[] clusterMap;
                bool[] isShapedAlone;
                CanvasGlyphShaping[] glyphShaping;

                // Evaluate which glyphs comprise the text.
                formattingSpan.Glyphs = textAnalyzer.GetGlyphs(
                    formattingSpanHelper.Range,
                    formattingSpanHelper.ScaledFont.FontFace,
                    fontSize,
                    false, // isSideways
                    formattingSpanHelper.Bidi.ResolvedLevel % 2 == 1,
                    formattingSpanHelper.Script,
                    "",
                    null,
                    null,
                    out clusterMap,
                    out isShapedAlone,
                    out glyphShaping);

                formattingSpan.FontFace = formattingSpanHelper.ScaledFont.FontFace;
                formattingSpan.FontSize = fontSize;
                formattingSpan.Script = formattingSpanHelper.Script;
                formattingSpan.ClusterMap = clusterMap;
                formattingSpan.GlyphShaping = glyphShaping;
                formattingSpan.Range = formattingSpanHelper.Range;
                formattingSpan.BidiLevel = formattingSpanHelper.Bidi.ResolvedLevel;
                formattingSpan.ForegroundColor = formattingSpanHelper.Foreground;
                formattingSpan.NeedsAdditionalJustificationCharacters = scriptProperties.JustificationCharacter != null;

                formattingSpans.Add(formattingSpan);

                if (formattingSpan.Glyphs.Length == formattingSpanHelper.Range.CharacterCount) //グリフ数と文字数が一致しないことがある
                {
                    for (int i = 0; i < formattingSpanHelper.Range.CharacterCount; i++)
                    {
                        var charIndex = formattingSpanHelper.Range.CharacterIndex + i;
                        if (text[charIndex] == '\t')
                        {
                            formattingSpan.Glyphs[i].Advance = TabWidth - posx % TabWidth;
                        }
                        posx += formattingSpan.Glyphs[i].Advance;
                    }
                }

                //
                // For text which contains non-uniform font faces, CanvasTextLayout takes the maximum of
                // all of line spacings and applies it as the overall line spacing. We do the same thing, here.
                //
                maxLineSpacing = System.Math.Max(maxLineSpacing, GetLineSpacing(formattingSpan.FontFace, fontSize));

                formattingSpanHelper.MoveNext();
            }

            return formattingSpans;
        }

        class GlyphRun
        {
            public FormattingSpan FormattingSpan;
            public List<CanvasGlyph> Glyphs;

            private int firstGlyphIndex; // Indices within the formatting span.
            private int lastGlyphIndex;

            public CanvasCharacterRange GetRange()
            {
                int formattingSpanStartIndex = FormattingSpan.Range.CharacterIndex;
                int start, end;
                GetCharacterIndex(firstGlyphIndex, FormattingSpan.ClusterMap,false ,out start);
                if (!GetCharacterIndex(lastGlyphIndex, FormattingSpan.ClusterMap, true ,out end))
                    end = FormattingSpan.ClusterMap.Length - 1;
                int length = end - start + 1;

                return new CanvasCharacterRange
                {
                    CharacterIndex = formattingSpanStartIndex + start,
                    CharacterCount = length
                };
            }

            public void AddGlyph(int glyphIndex)
            {
                if (Glyphs.Count == 0)
                {
                    firstGlyphIndex = glyphIndex;
                }
                lastGlyphIndex = glyphIndex;

                Glyphs.Add(FormattingSpan.Glyphs[glyphIndex]);
            }

            public int[] GetClusterMap(CanvasCharacterRange range)
            {
                //
                // Create a cluster map for this character range. Because the cluster map
                // should reflect only the text positions in the range, we need to re-normalize
                // it (so that it starts at 0).
                //
                int[] clusterMap = new int[range.CharacterCount];

                int formattingSpanStartIndex = FormattingSpan.Range.CharacterIndex;

                int firstClusterMapValue = FormattingSpan.ClusterMap[range.CharacterIndex - formattingSpanStartIndex];

                for (int i = 0; i < range.CharacterCount; ++i)
                {
                    int indexWithinFormattingSpan = range.CharacterIndex - formattingSpanStartIndex + i; // Cluster maps are per formatting span.

                    clusterMap[i] = FormattingSpan.ClusterMap[indexWithinFormattingSpan] - firstClusterMapValue;
                }
                return clusterMap;
            }

            public CanvasGlyphShaping[] GetShaping()
            {
                //
                // The shaping array is in terms of glyph indices. The formatting span contains all the shaping info for this glyph run.
                //
                CanvasGlyphShaping[] shaping = new CanvasGlyphShaping[Glyphs.Count];

                for (int i = 0; i < Glyphs.Count; ++i)
                {
                    shaping[i] = FormattingSpan.GlyphShaping[firstGlyphIndex + i];
                }
                return shaping;
            }

            public float GetAdvance()
            {
                float advance = 0;
                foreach (var g in Glyphs)
                {
                    advance += g.Advance;
                }
                return advance;
            }
        }

        class LayoutBox
        {
            List<GlyphRun> glyphRuns;
            Rect rectangle;
            uint highestBidiLevel = 0;
            uint lowestOddBidiLevel = uint.MaxValue;

            public LayoutBox(Rect r)
            {
                glyphRuns = new List<GlyphRun>();
                rectangle = r;
            }

            public void AddGlyphRun(GlyphRun run)
            {
                glyphRuns.Add(run);

                uint bidiLevel = run.FormattingSpan.BidiLevel;
                highestBidiLevel = Math.Max(highestBidiLevel, bidiLevel);

                if (bidiLevel % 2 == 1)
                    lowestOddBidiLevel = Math.Min(lowestOddBidiLevel, run.FormattingSpan.BidiLevel);
            }

            public List<GlyphRun> GlyphRuns { get { return glyphRuns; } }
            public Rect Rectangle { get { return rectangle; } }

            public int[] BidiOrdering
            {
                get;
                private set;
            }

            public int GetGlyphCount()
            {
                int count = 0;
                foreach (var g in glyphRuns)
                {
                    count += g.Glyphs.Count;
                }
                return count;
            }

            public void ProduceBidiOrdering()
            {
                int spanStart = 0;

                int spanCount = GlyphRuns.Count;

                //
                // Produces an index mapping from sequential order to visual bidi order.
                // The function progresses forward, checking the bidi level of each
                // pair of spans, reversing when needed.
                //
                // See the Unicode technical report 9 for an explanation.
                // http://www.unicode.org/reports/tr9/tr9-17.html 
                //
                int[] spanIndices = new int[spanCount];

                for (int i = 0; i < spanCount; ++i)
                    spanIndices[i] = spanStart + i;

                if (spanCount <= 1)
                {
                    this.BidiOrdering = spanIndices;
                    return;
                }

                int runStart = 0;
                uint currentLevel = glyphRuns[spanStart].FormattingSpan.BidiLevel;

                // Rearrange each run to produced reordered spans.
                for (int i = 0; i < spanCount; ++i)
                {
                    int runEnd = i + 1;
                    uint nextLevel = (runEnd < spanCount) ? glyphRuns[spanIndices[runEnd]].FormattingSpan.BidiLevel : 0;

                    if (currentLevel <= nextLevel)
                    {
                        if (currentLevel < nextLevel)
                        {
                            currentLevel = nextLevel;
                            runStart = i + 1;
                        }
                        continue; // Skip past equal levels, or increasing stairsteps.
                    }

                    do
                    {
                        // Recede to find start of the run and previous level.
                        uint previousLevel;
                        for (; ; )
                        {
                            if (runStart <= 0)
                            {
                                previousLevel = 0; // position before string has bidi level of 0
                                break;
                            }
                            if (glyphRuns[spanIndices[--runStart]].FormattingSpan.BidiLevel < currentLevel)
                            {
                                previousLevel = glyphRuns[spanIndices[runStart]].FormattingSpan.BidiLevel;
                                ++runStart; // compensate for going one element past
                                break;
                            }
                        }

                        // Reverse the indices, if the difference between the current and
                        // next/previous levels is odd. Otherwise, it would be a no-op, so
                        // don't bother.
                        if (Math.Min(currentLevel - nextLevel, currentLevel - previousLevel) % 2 != 0)
                        {
                            ReverseArrayElements(spanIndices, runStart, runEnd);
                        }

                        // Descend to the next lower level, the greater of previous and next
                        currentLevel = Math.Max(previousLevel, nextLevel);
                    } while (currentLevel > nextLevel);
                }

                this.BidiOrdering = spanIndices;
            }

            void ReverseArrayElements(int[] indices, int start, int end)
            {
                int length = end - start;
                for (int i = 0; i < length / 2; i++)
                {
                    int temp = indices[start + i];
                    indices[start + i] = indices[end - i - 1];
                    indices[end - i - 1] = temp;
                }
            }
        }

        // This method returns the current glyph run.
        GlyphRun BeginGlyphRun(Rect rectangle, float advance, LayoutBox currentLayoutBox, FormattingSpan formattingSpan, int lineNumber)
        {
            GlyphRun glyphRun = new GlyphRun();

            glyphRun.FormattingSpan = formattingSpan;

            glyphRun.Glyphs = new List<CanvasGlyph>();
            currentLayoutBox.AddGlyphRun(glyphRun);

            return glyphRun;
        }

        //
        // Returns the current glyph run, or null if there's no more layout boxes.
        //
        GlyphRun BeginNewLayoutBox(ref int rectangleIndex, List<Rect> rectangles, ref float glyphRunAdvance, ref int wordsPerLine, FormattingSpan formattingSpan, List<LayoutBox> layoutBoxes)
        {
            rectangleIndex++;
            if (rectangleIndex >= rectangles.Count)
                return null;

            LayoutBox layoutBox = new LayoutBox(rectangles[rectangleIndex]);
            layoutBoxes.Add(layoutBox);

            glyphRunAdvance = 0;
            wordsPerLine = 0;

            GlyphRun newGlyphRun = BeginGlyphRun(rectangles[rectangleIndex], glyphRunAdvance, layoutBox, formattingSpan, rectangleIndex);

            return newGlyphRun;
        }

        static bool GetCharacterIndex(int glyphIndex, int[] clusterMap, bool isForward,out int correspondingTextPosition)
        {
            bool result = false;
            correspondingTextPosition = 0;
            for (int k = 0; k < clusterMap.Length; ++k)
            {
                if (clusterMap[k] == glyphIndex)
                {
                    correspondingTextPosition = k;
                    result = true;
                    if (!isForward)
                        return result;
                }
            }
            return result;
        }

        List<LayoutBox> CreateGlyphRuns(List<Rect> rectangles, List<FormattingSpan> formattingSpans, CanvasAnalyzedBreakpoint[] analyzedBreakpoints)
        {
            List<LayoutBox> layoutBoxes = new List<LayoutBox>();

            if (rectangles.Count == 0) return layoutBoxes;

            int rectangleIndex = -1;
            float glyphRunAdvance = 0;
            int wordsPerLine = 0;

            for (int formattingSpanIndex = 0; formattingSpanIndex < formattingSpans.Count; formattingSpanIndex++)
            {
                var formattingSpan = formattingSpans[formattingSpanIndex];

                GlyphRun currentGlyphRun;
                if (layoutBoxes.Count == 0)
                    currentGlyphRun = BeginNewLayoutBox(ref rectangleIndex, rectangles, ref glyphRunAdvance, ref wordsPerLine, formattingSpan, layoutBoxes);
                else
                    currentGlyphRun = BeginGlyphRun(rectangles[rectangleIndex], glyphRunAdvance, layoutBoxes[layoutBoxes.Count - 1], formattingSpan, rectangleIndex);

                if (currentGlyphRun == null)
                    return layoutBoxes;

                var glyphs = formattingSpan.Glyphs;

                for (int i = 0; i < glyphs.Length; ++i)
                {
                    //
                    // Will the next word fit in the box?
                    //
                    float wordAdvance = 0.0f;
                    int wordBoundary;
                    for (wordBoundary = i; wordBoundary < glyphs.Length; wordBoundary++)
                    {
                        int correspondingTextPosition;
                        if (!GetCharacterIndex(wordBoundary, formattingSpan.ClusterMap , true , out correspondingTextPosition))
                            correspondingTextPosition = wordBoundary;
                        correspondingTextPosition += formattingSpan.Range.CharacterIndex;

                        var afterThisCharacter = analyzedBreakpoints[correspondingTextPosition].BreakAfter;
                        var beforeNextCharacter = (correspondingTextPosition < analyzedBreakpoints.Length - 1) ? analyzedBreakpoints[correspondingTextPosition + 1].BreakBefore : CanvasLineBreakCondition.Neutral;

                        // 
                        // The text for this demo doesn't have any hard line breaks.
                        //
                        System.Diagnostics.Debug.Assert(afterThisCharacter != CanvasLineBreakCondition.MustBreak);

                        if (afterThisCharacter == CanvasLineBreakCondition.CanBreak && beforeNextCharacter != CanvasLineBreakCondition.CannotBreak)
                            break;

                        wordAdvance += glyphs[wordBoundary].Advance;
                    }

                    if (glyphRunAdvance + wordAdvance < rectangles[rectangleIndex].Width) // It fits
                    {
                        for (int j = i; j <= wordBoundary; j++)
                        {
                            if (j < glyphs.Length)
                            {
                                currentGlyphRun.AddGlyph(j);

                                glyphRunAdvance += glyphs[j].Advance;
                            }
                        }
                        i = wordBoundary;
                        wordsPerLine++;
                    }
                    else // Doesn't fit
                    {
                        if (wordsPerLine == 0) // Need an emegency break?
                        {
                            int breakBoundary = i;
                            while (breakBoundary < glyphs.Length && glyphRunAdvance + glyphs[breakBoundary].Advance < rectangles[rectangleIndex].Width)
                            {
                                currentGlyphRun.AddGlyph(breakBoundary);

                                glyphRunAdvance += glyphs[breakBoundary].Advance;

                                breakBoundary++;
                            }
                            i = breakBoundary - 1;
                        }
                        else
                        {
                            i--; // Retry the glyph against the next rectangle.
                        }

                        currentGlyphRun = BeginNewLayoutBox(ref rectangleIndex, rectangles, ref glyphRunAdvance, ref wordsPerLine, formattingSpan, layoutBoxes);

                        if (currentGlyphRun == null)
                            return layoutBoxes;
                    }
                }
            }

            return layoutBoxes;
        }

        CanvasJustificationOpportunity[] GetJustificationOpportunities(CanvasTextAnalyzer textAnalyzer, LayoutBox layoutBox, out CanvasGlyph[] allGlyphs)
        {
            int layoutBoxGlyphCount = layoutBox.GetGlyphCount();

            CanvasJustificationOpportunity[] justificationOpportunities = new CanvasJustificationOpportunity[layoutBoxGlyphCount];
            allGlyphs = new CanvasGlyph[layoutBoxGlyphCount];

            int glyphIndex = 0;

            for (int i = 0; i < layoutBox.GlyphRuns.Count; i++)
            {
                if (layoutBox.GlyphRuns[i].Glyphs.Count == 0)
                    continue;

                CanvasCharacterRange range = layoutBox.GlyphRuns[i].GetRange();

                var glyphRunClusterMap = layoutBox.GlyphRuns[i].GetClusterMap(range);
                var glyphRunShaping = layoutBox.GlyphRuns[i].GetShaping();

                var justificationOpportunitiesThisGlyphRun = textAnalyzer.GetJustificationOpportunities(
                    range,
                    layoutBox.GlyphRuns[i].FormattingSpan.FontFace,
                    layoutBox.GlyphRuns[i].FormattingSpan.FontSize,
                    layoutBox.GlyphRuns[i].FormattingSpan.Script,
                    glyphRunClusterMap,
                    glyphRunShaping);

                for (int j = 0; j < layoutBox.GlyphRuns[i].Glyphs.Count; ++j)
                {
                    justificationOpportunities[glyphIndex + j] = justificationOpportunitiesThisGlyphRun[j];
                    allGlyphs[glyphIndex + j] = layoutBox.GlyphRuns[i].Glyphs[j];
                }
                glyphIndex += layoutBox.GlyphRuns[i].Glyphs.Count;
            }

            return justificationOpportunities;
        }

        void SplitJustifiedGlyphsIntoRuns(CanvasTextAnalyzer textAnalyzer, LayoutBox layoutBox, CanvasGlyph[] justifiedGlyphs, bool needsAdditionalJustificationCharacters)
        {
            int glyphIndex = 0;

            float xPosition = (float)layoutBox.Rectangle.Right;
            for (int i = 0; i < layoutBox.GlyphRuns.Count; i++)
            {
                if (layoutBox.GlyphRuns[i].Glyphs.Count == 0)
                    continue;

                int originalGlyphCountForThisRun = layoutBox.GlyphRuns[i].Glyphs.Count;

                if (needsAdditionalJustificationCharacters)
                {
                    // Replace the glyph data, since justification can modify glyphs                
                    CanvasGlyph[] justifiedGlyphsForThisGlyphRun = new CanvasGlyph[layoutBox.GlyphRuns[i].Glyphs.Count];
                    for (int j = 0; j < layoutBox.GlyphRuns[i].Glyphs.Count; j++)
                    {
                        justifiedGlyphsForThisGlyphRun[j] = justifiedGlyphs[glyphIndex + j];
                    }

                    CanvasCharacterRange range = layoutBox.GlyphRuns[i].GetRange();

                    var glyphRunClusterMap = layoutBox.GlyphRuns[i].GetClusterMap(range);
                    var glyphRunShaping = layoutBox.GlyphRuns[i].GetShaping();

                    CanvasGlyph[] newSetOfGlyphs = textAnalyzer.AddGlyphsAfterJustification(
                        layoutBox.GlyphRuns[i].FormattingSpan.FontFace,
                        layoutBox.GlyphRuns[i].FormattingSpan.FontSize,
                        layoutBox.GlyphRuns[i].FormattingSpan.Script,
                        glyphRunClusterMap,
                        layoutBox.GlyphRuns[i].Glyphs.ToArray(),
                        justifiedGlyphsForThisGlyphRun,
                        glyphRunShaping);

                    layoutBox.GlyphRuns[i].Glyphs = new List<CanvasGlyph>(newSetOfGlyphs);
                }
                else
                {
                    for (int j = 0; j < layoutBox.GlyphRuns[i].Glyphs.Count; j++)
                    {
                        layoutBox.GlyphRuns[i].Glyphs[j] = justifiedGlyphs[glyphIndex + j];
                    }
                }

                glyphIndex += originalGlyphCountForThisRun;
            }
        }

        void JustifyLine(CanvasTextAnalyzer textAnalyzer, LayoutBox layoutBox)
        {
            CanvasGlyph[] allGlyphs;
            var justificationOpportunities = GetJustificationOpportunities(textAnalyzer, layoutBox, out allGlyphs);

            CanvasGlyph[] justifiedGlyphs = textAnalyzer.ApplyJustificationOpportunities(
                (float)layoutBox.Rectangle.Width,
                justificationOpportunities,
                allGlyphs);

            bool needsJustificationCharacters = layoutBox.GlyphRuns[0].FormattingSpan.NeedsAdditionalJustificationCharacters;

            SplitJustifiedGlyphsIntoRuns(textAnalyzer, layoutBox, justifiedGlyphs, needsJustificationCharacters);
        }

        void Justify(CanvasTextAnalyzer textAnalyzer, List<LayoutBox> layoutBoxes)
        {
            if (layoutBoxes.Count == 0)
                return;

            for (int i = 0; i < layoutBoxes.Count; i++)
            {
                if (layoutBoxes[i].GlyphRuns.Count == 0)
                    return;

                JustifyLine(textAnalyzer, layoutBoxes[i]);
            }
        }

        List<Rect> SplitGeometryIntoRectangles(float rectangleHeight)
        {
            List<Rect> result = new List<Rect>();

            var geometryBounds = this.RequireSize;
            double left = 0;
            double top = 0;
            double y = top;
            while (y < geometryBounds.Height)
            {
                var lineRegion = new Rect(left, y, geometryBounds.Width, rectangleHeight);
                result.Add(lineRegion);
                y += rectangleHeight;

            }

            return result;
        }

        IReadOnlyList<KeyValuePair<CanvasCharacterRange,T>> GetT<T>(IList<KeyValuePair<CanvasCharacterRange, T>> list, int textlength,T defalutValue)
        {
            List<KeyValuePair<CanvasCharacterRange, T>> result = new List<KeyValuePair<CanvasCharacterRange, T>>();

            if(list == null || list.Count == 0)
            {
                result.Add(new KeyValuePair<CanvasCharacterRange, T>(
                    new CanvasCharacterRange() { CharacterIndex = 0, CharacterCount = textlength },
                    defalutValue
                    ));
            }

            int j = 0;
            for(int i = 0; i < list.Count; i++)
            {
                var current = list[i];
                if (j < current.Key.CharacterIndex)
                {
                    result.Add(new KeyValuePair<CanvasCharacterRange, T>(
                        new CanvasCharacterRange() { CharacterIndex = j, CharacterCount = current.Key.CharacterIndex - j },
                        defalutValue));
                }
                result.Add(new KeyValuePair<CanvasCharacterRange, T>(
                    new CanvasCharacterRange() { CharacterIndex = current.Key.CharacterIndex, CharacterCount = current.Key.CharacterCount },
                    current.Value));
                j = current.Key.CharacterIndex + current.Key.CharacterCount;
            }

            if(j < textlength)
            {
                result.Add(new KeyValuePair<CanvasCharacterRange, T>(
                    new CanvasCharacterRange() { CharacterIndex = j, CharacterCount = textlength - j },
                    defalutValue));
            }
            return result;
        }

        private void EnsureLayout()
        {
            if (!needsLayout)
                return;

            var textFormat = this.TextFormat;

            CanvasTextAnalyzer textAnalyzer = new CanvasTextAnalyzer(text, TextDirection);

            //
            // Figure out what fonts to use.
            //
            var fontResult = textAnalyzer.GetFonts(textFormat);

            //
            // Perform a script analysis on the text.
            //
            var scriptAnalysis = textAnalyzer.GetScript();

            //
            // Perform bidi analysis.
            //
            var bidiAnalysis = textAnalyzer.GetBidi();

            var forgorundColorAnalysis = this.GetT<CanvasSolidColorBrush>(this.foregroundColors, text.Length, this.DefaultForegorundBrush);

            float maxLineSpacing = 0;
            List<FormattingSpan> formattingSpans = EvaluateFormattingSpans(text, textAnalyzer, fontResult, scriptAnalysis, bidiAnalysis, forgorundColorAnalysis, out maxLineSpacing);

            //
            // Perform line break analysis.
            //
            var breakpoints = textAnalyzer.GetBreakpoints();

            //
            // Get the rectangles to layout text into.
            //
            layoutRectangles = SplitGeometryIntoRectangles(maxLineSpacing);

            //
            // Insert glyph runs into the layout boxes.
            //
            layoutBoxes = CreateGlyphRuns(layoutRectangles, formattingSpans, breakpoints);

            _ActualSize = new Size(this.RequireSize.Width, layoutBoxes.Count * maxLineSpacing);

            if (ShouldJustify)
            {
                Justify(textAnalyzer, layoutBoxes);
            }

            foreach (var l in layoutBoxes)
                l.ProduceBidiOrdering();

            needsLayout = false;

        }

        private static float GetLineSpacing(CanvasFontFace fontFace, float fontSize)
        {
            return (fontFace.LineGap + fontFace.Ascent + fontFace.Descent) * fontSize;
        }

        public void SetForgroundColor(CanvasCharacterRange range, CanvasSolidColorBrush brush)
        {
            this.foregroundColors.Add(new KeyValuePair<CanvasCharacterRange, CanvasSolidColorBrush>(range, brush));
        }

        public int GetCaretPosition(int index,bool trailing,out CanvasTextLayoutRegion region)
        {
            var regions = this.GetCharacterRegions();
            region = regions.Where((r) => {
                return index >= r.CharacterIndex && index < r.CharacterIndex + r.CharacterCount;
            }).First();

            if (trailing)
                return region.CharacterIndex + region.CharacterCount - 1;
            else
                return region.CharacterIndex;
        }

        public IEnumerable<CanvasTextLayoutRegion> GetCharacterRegions(int start,int length)
        {
            int end = start + length - 1;
            return this.GetCharacterRegions().Where((r) => {
                var region_end = r.CharacterIndex + r.CharacterCount - 1;
                return (r.CharacterIndex >= start &&  region_end <= end) ||
                        (start >= r.CharacterIndex && end <= region_end) ||
                        (start >= r.CharacterIndex &&  start <= region_end) ||
                        (end >= r.CharacterIndex && end <= region_end);
            });
        }

        public IEnumerable<CanvasTextLayoutRegion> GetCharacterRegions()
        {
            EnsureLayout();

            float posy = 0;
            foreach (var box in layoutBoxes)
            {
                float posx = 0;
                foreach(var glyphRunIndex in box.BidiOrdering)
                {
                    var run = box.GlyphRuns[glyphRunIndex];

                    if (run.Glyphs.Count == 0)
                        continue;

                    var range = run.GetRange();
                    var clusterMap = run.GetClusterMap(range);
                    var result = new CanvasTextLayoutRegion();
                    int previousCluster = 0, sameClusterCount = 0 ;
                    float advanceWidth = 0;
                    for(int i = 0; i < clusterMap.Length; i++)
                    {
                        var cluster = clusterMap[i];
                        if(i == 0)
                        {
                            previousCluster = cluster;
                            sameClusterCount = 1;
                            advanceWidth = 0;
                        }
                        else if (previousCluster != cluster)
                        {
                            result.CharacterIndex = range.CharacterIndex + previousCluster;
                            result.CharacterCount = sameClusterCount;
                            result.LayoutBounds = new Rect()
                            {
                                X = posx,
                                Y = posy,
                                Width = advanceWidth,
                                Height = box.Rectangle.Height,
                            };
                            yield return result;
                            posx += advanceWidth;
                            advanceWidth = 0;
                            sameClusterCount = 1;
                            previousCluster = cluster;
                        }
                        else
                        {
                            sameClusterCount++;
                        }
                        if (i < run.Glyphs.Count)
                            advanceWidth += run.Glyphs[i].Advance;
                    }
                    if(sameClusterCount > 0)
                    {
                        result.CharacterIndex = range.CharacterIndex + previousCluster;
                        result.CharacterCount = sameClusterCount;
                        result.LayoutBounds = new Rect()
                        {
                            X = posx,
                            Y = posy,
                            Width = advanceWidth,
                            Height = box.Rectangle.Height,
                        };
                        yield return result;
                        posx += advanceWidth;
                    }
                }
                posy += (float)box.Rectangle.Height;
            }
        }

        public bool HitText(double x, double y, out CanvasTextLayoutRegion region)
        {
            var regions = this.GetCharacterRegions().Where((r) => {
                return x >= r.LayoutBounds.Left && x < r.LayoutBounds.Right && y >= r.LayoutBounds.Top && y < r.LayoutBounds.Bottom;
            });
            region = regions.FirstOrDefault();
            if (regions.Count() > 0)
                return true;
            else
                return false;
        }

        public void Draw(CanvasDrawingSession DrawingSession,float posx,float posy)
        {
            EnsureLayout();

            if (this.DefaultForegorundBrush == null)
                return;

            foreach (LayoutBox l in layoutBoxes)
            {
                if (l.GlyphRuns.Count > 0)
                {
                    float layoutAdvance = 0;
                    foreach (var g in l.GlyphRuns)
                    {
                        layoutAdvance += g.GetAdvance();
                    }

                    float x = (float)l.Rectangle.Left + posx;

                    if (TextDirection == CanvasTextDirection.RightToLeftThenBottomToTop)
                        x = (float)l.Rectangle.Right - layoutAdvance;

                    foreach (int glyphRunIndex in l.BidiOrdering)
                    {
                        GlyphRun g = l.GlyphRuns[glyphRunIndex];

                        if (g.Glyphs.Count > 0)
                        {
                            float advance = g.GetAdvance();
                            Vector2 position;
                            position.X = x;
                            position.Y = posy;
                            //
                            // The Arabic test string contains control characters. A typical text renderer will just not draw these.
                            //
                            if (g.FormattingSpan.Script.Shape == CanvasScriptShape.NoVisual)
                            {
                                if(IsDrawControlCode == false) 
                                {
                                }
                                else
                                {
                                    //コントロールコードを描く
                                    var s = this.text.Substring(g.FormattingSpan.Range.CharacterIndex, g.FormattingSpan.Range.CharacterCount);
                                    byte[] bytes = new byte[s.Length * sizeof(char)];
                                    System.Buffer.BlockCopy(s.ToCharArray(), 0, bytes, 0, bytes.Length);

                                    var formatted_str = new System.Text.StringBuilder();
                                    for(int i = 0; i < bytes.Length; i++)
                                    {
                                        formatted_str.AppendFormat("{0:00}\n", bytes[i]);
                                    }

                                    var controlTextFormat = new CanvasTextFormat();
                                    controlTextFormat.FontFamily = this.TextFormat.FontFamily;
                                    controlTextFormat.FontSize = this.TextFormat.FontSize / 4;

                                    var height = (float)l.Rectangle.Bottom - 4;
                                    var textlayout = new CanvasTextLayout(DrawingSession, formatted_str.ToString(), controlTextFormat, advance - 4, height);
                                    DrawingSession.DrawTextLayout(textlayout, new Vector2(position.X + 4, position.Y + 4), g.FormattingSpan.ForegroundColor);
                                    DrawingSession.DrawRectangle(position.X + 2, position.Y + 2, advance - 4, height, g.FormattingSpan.ForegroundColor);

                                    controlTextFormat.Dispose();
                                }
                            }
                            else
                            {
                                if (g.FormattingSpan.BidiLevel % 2 != 0)
                                    position.X += advance;

                                position.Y += (float)l.Rectangle.Bottom;

                                DrawingSession.DrawGlyphRun(
                                    position,
                                    g.FormattingSpan.FontFace,
                                    g.FormattingSpan.FontSize,
                                    g.Glyphs.ToArray(),
                                    false, // isSideways
                                    g.FormattingSpan.BidiLevel,
                                    g.FormattingSpan.ForegroundColor);
                            }

                            x += advance;
                        }
                    }
                }
            }
        }
    }
}