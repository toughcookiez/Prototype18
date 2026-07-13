using System.Collections.Generic;
using System.Linq;
using IconsCreationTool.Editor.Utility.Extensions;
using UnityEngine;

namespace IconsCreationTool.Editor.Core
{
    public struct IconsCreatorData
    {
        public int Size { get; }
        public float Padding { get; }
        public string Prefix { get; }
        public string Suffix { get; }
        public IconBackgroundData BackgroundData { get; }
        public Texture2D FrameTexture { get; }
        public GameObject[] Targets { get; }
        public bool RenderShadows { get; }
        public Vector3 Rotation { get; }
        public Vector3 Position { get; }


        public IconsCreatorData(int size, float padding, string prefix, string suffix,
            IconBackgroundData backgroundData, Texture2D frameTexture, List<Object> targets, bool renderShadows, Vector3 rotation, Vector3 position)
        {
            Size = size;
            Padding = padding;
            Prefix = prefix;
            Suffix = suffix;
            BackgroundData = backgroundData;
            FrameTexture = frameTexture;
            Targets = targets.ExtractAllGameObjects().Where(g => g.HasVisibleMesh()).ToArray();
            RenderShadows = renderShadows;
            Rotation = rotation;
            Position = position;
        }
    }
}