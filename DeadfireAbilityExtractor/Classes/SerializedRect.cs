using Newtonsoft.Json;
using UnityEngine;

namespace DeadfireAbilityExtractor
{
    [JsonObject(MemberSerialization.OptOut)]
    public struct SerializedRect
    {
        [System.NonSerialized, JsonIgnore]
        private Rect rect;

        public float x
        {
            get { return rect.x; }
            set { rect.x = value; }
        }

        public float y
        {
            get { return rect.y; }
            set { rect.y = value; }
        }

        public float width
        {
            get { return rect.width; }
            set { rect.width = value; }
        }

        public float height
        {
            get { return rect.height; }
            set { rect.height = value; }
        }

        public static implicit operator SerializedRect(Rect rect)
        {
            return new SerializedRect() { rect = rect };
        }
    }
}