#if UNITY_EDITOR

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
                { "/", SetColor },
                { "/index.html", SetColor},
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
        </head>
        <body style='text-align:center'>
            <h1>Hello world!</h1>
            <br>
            <div>
                <form method='post' action='/'>
                    <label for='colorWell'>Color: </label>
                    <input id='colorWell' type='color' name='color' value='#{0}' onchange='this.form.submit()'></input>
                </form>
            </div>
        </body>
    </html>";

            return new HTTP.Server.Response("text/html", string.Format(page, ColorUtility.ToHtmlStringRGB(material.color)));
        }

        protected HTTP.Server.Response SetColor(Dictionary<string, string> parameters)
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

            return MainPage(parameters);
        }
        #endregion
    }
}
#endif // UNITY_EDITOR
