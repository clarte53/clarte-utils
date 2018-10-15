#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CLARTE.HTTP.Example
{
    public class Server : MonoBehaviour
    {
        #region Members
        protected GameObject sphere;
        protected Material material;
        #endregion

        #region MonoBehaviour callbacks
        protected void Start()
        {
            sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            sphere.transform.position = Vector3.zero;

            material = sphere.GetComponent<MeshRenderer>().material;

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
                #parameters {{ margin-left: 45%; }}
            </style>
        </head>
        <body>
            <h1>Hello world!</h1>
            <br>
            <div id='parameters'>
                <form method='post' action='/'>
                    <ul>
                        <li>
                            <label>Color: </label>
                            <input type='color' name='color' value='#{0}' onchange='this.form.submit()'></input>
                        </li>
                        <li>
                            <label>Show: </label>
                            <input type='checkbox' name='show' value='True' {1} onchange='this.form.submit()'></input>
                        </li>
                        <li>
                            <button type='submit' name='anim' value='True'>Play animation</button>
                        </li>
                    </ul>
                </form>
            </div>
        </body>
    </html>";

            return new HTTP.Server.Response("text/html", string.Format(page, ColorUtility.ToHtmlStringRGB(material.color), sphere.activeSelf ? "checked" : ""));
        }

        protected HTTP.Server.Response UpdateHTML(Dictionary<string, string> parameters)
        {
            string color_str;
            Color color;

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
            if(parameters.Count > 0)
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
#endif // UNITY_EDITOR
