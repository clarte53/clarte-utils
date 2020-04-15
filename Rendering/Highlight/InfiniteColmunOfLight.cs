using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CLARTE.Rendering.Highlight
{
    public class InfiniteColmunOfLight : IHighlight
    {
        #region Members
        public GameObject column;
        public float sizeOnScreen = 500;
        Vector3 startScale;
        #endregion

        #region IHighlight implementation
        public override void SetHighlightEnabled(bool enabled)
        {
            column?.SetActive(enabled);
        }
        #endregion

        #region MonoBehaviour callbacks
        // Start is called before the first frame update
        void Awake()
        {
            if (column != null)
            {
                column.SetActive(false);
                startScale = column.transform.localScale;
            }
        }

        void Update()
        {
            if (!column && !column.activeSelf)
                return;

            Vector3 a = Camera.main.WorldToScreenPoint(column.transform.position);
            Vector3 b = new Vector3(a.x, a.y + sizeOnScreen, a.z);

            Vector3 aa = Camera.main.ScreenToWorldPoint(a);
            Vector3 bb = Camera.main.ScreenToWorldPoint(b);

            column.transform.localScale = startScale * (aa - bb).magnitude;
        }
        #endregion
    }
}
