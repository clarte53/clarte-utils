#if UNITY_EDITOR && !NETFX_CORE

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CLARTE.Net.HTTP.Example
{
    public class Server : MonoBehaviour
    {
        #region Members
        protected GameObject sphere;
        protected Material material;
        protected Text text;
        protected Text number;
        #endregion

        #region MonoBehaviour callbacks
        protected void Start()
        {
            const float field_height = 50f;
            const int font_size = 36;
            
            sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            material = sphere.GetComponent<MeshRenderer>().material;

            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            Canvas canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.localScale = 0.001f * Vector3.one;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

            text = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.rectTransform.SetParent(canvas.transform, false);
            text.rectTransform.anchorMin = new Vector2(0f, 1f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.pivot = new Vector2(0.5f, 1f);
            text.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            text.rectTransform.sizeDelta = new Vector2(0f, field_height);
            text.fontSize = font_size;
            text.font = font;
            text.text = "Text";

            number = new GameObject("Number", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            number.rectTransform.SetParent(canvas.transform, false);
            number.rectTransform.anchorMin = new Vector2(0f, 1f);
            number.rectTransform.anchorMax = new Vector2(1f, 1f);
            number.rectTransform.pivot = new Vector2(0.5f, 1f);
            number.rectTransform.anchoredPosition = new Vector2(0f, -field_height);
            number.rectTransform.sizeDelta = new Vector2(0f, field_height);
            number.fontSize = font_size;
            number.font = font;
            number.text = "0";

            new HTTP.Server(8080, new Dictionary<string, HTTP.Server.Endpoint>
            {
                { "/", UpdateHTML },
                { "/index.html", UpdateHTML},
            });
        }
        #endregion

        #region HTML pages handling
        protected HTTP.Server.Response MainPage(Dictionary<string, string> parameters)
        {
            const string page = @"<!DOCTYPE HTML PUBLIC ' -//IETF//DTD HTML 2.0//EN\'>
    <html>
        <head>
            <title>Demo Unity HTTP API</title>
            <style>
                h1 {{ text-align: center; }}
                .parameters {{ margin-left: 45%; }}
            </style>
        </head>
        <body>
            <h1>Hello world!</h1>
            <br>
            <div class='parameters'>
                <ul>
                    <form method='post' action='/'>
                        <li>
                            <label>Text: </label>
                            <input type='text' name='text' value='{0}' onchange='this.form.submit()'></input>
                        </li>
                        <li>
                            <label>Number: </label>
                            <input type='range' name='number' min='0' max='10' value='{1}' onchange='this.form.submit()'></input>
                        </li>
                        <li>
                            <label>Color: </label>
                            <input type='color' name='color' value='#{2}' onchange='this.form.submit()'></input>
                        </li>
                        <li>
                            <label>Show: </label>
                            <input type='checkbox' name='show' value='True' {3} onchange='this.form.submit()'></input>
                        </li>
                    </form>
                    <form method='post' action='/'>
                        <li>
                            <button type='submit' name='anim' value='True'>Play animation</button>
                        </li>
                    </form>
                </ul>
            </div>
        </body>
    </html>";

            return new HTTP.Server.Response("text/html", string.Format(page, text.text, number.text, ColorUtility.ToHtmlStringRGB(material.color), sphere.activeSelf ? "checked" : ""));
        }

        protected HTTP.Server.Response UpdateHTML(Dictionary<string, string> parameters)
        {
            string text_str, number_str, color_str;
            int number_value;
            Color color;

            if(parameters.TryGetValue("text", out text_str))
            {
                text.text = text_str;
            }

            if(parameters.TryGetValue("number", out number_str) && int.TryParse(number_str, out number_value))
            {
                number.text = number_value.ToString(); ;
            }

            if(parameters.TryGetValue("color", out color_str))
            {
                if(ColorUtility.TryParseHtmlString(string.Format("#{0}", color_str.TrimStart('#')), out color))
                {
                    material.color = color;
                }
                else
                {
                    material.color = Color.white;
                }
            }

            // HTTP checkboxes are added to the parameters only if checked.
            // However, we must discriminate between refresh events (no parameters) and checkbox unchecked (some parameters but no parameter 'show')
            // As 'show' is in the same form as 'color', we can use it to check if the form has been submited (to discriminate with other forms as well)
            if(parameters.ContainsKey("color"))
            {
                sphere.SetActive(parameters.ContainsKey("show"));
            }

            if(parameters.ContainsKey("anim"))
            {
                StartCoroutine(Animation());
            }

            return MainPage(parameters);
        }
        #endregion

        #region Internal methods
        protected IEnumerator Animation()
        {
            const float length = 1.5f; // In seconds

            float start = Time.realtimeSinceStartup;
            float diff = 0f;

            do
            {
                diff = Time.realtimeSinceStartup - start;

                sphere.transform.localScale = (1f + Mathf.Sin(diff * Mathf.PI / length)) * Vector3.one;

                yield return null;
            }
            while(diff < length);
        }
        #endregion
    }
}

#endif // UNITY_EDITOR && !NETFX_CORE
