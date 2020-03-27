using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using UnityEngine;
using UnityEngine.Networking;

namespace CLARTE.Net.LMS
{
	public class Client : MonoBehaviour
	{
		#region Members
		private const string urlKey = "LMS_url";
		private const string organizationKey = "LMS_organization";

		public string defaultUrl = "https://localhost:5001";

		private string token;
		#endregion

		#region Getters / Setters
		public bool LoggedIn
		{
			get
			{
				return token != null;
			}
		}
		#endregion

		#region MonoBehaviour callbacks
		protected void Awake()
		{
			if(!PlayerPrefs.HasKey(urlKey))
			{
				SetLmsUrl(defaultUrl);
			}
		}
		#endregion

		#region Public API
		public string GetLmsUrl()
		{
			return PlayerPrefs.GetString(urlKey);
		}

		public void SetLmsUrl(string url)
		{
			PlayerPrefs.SetString(urlKey, url);
		}

		public string GetOrganization()
		{
			return PlayerPrefs.GetString(organizationKey);
		}

		public void SetOrganization(string organization)
		{
			PlayerPrefs.SetString(organizationKey, organization);
		}

		public void Logout()
		{
			token = null;
		}

		public void Login(string username, string password)
		{
			HttpGet<Entities.User>("login", user => token = user.token, new Dictionary<string, string>
			{
				{ "organization", PlayerPrefs.GetString(organizationKey) },
				{ "username", username },
				{ "password", password },
			});
		}

		public void Values()
		{
			HttpGet<List<string>>("values", l =>
			{
				if(l != null)
				{
					foreach(string s in l)
					{
						Debug.Log(s);
					}
				}
			}, null);
		}
		#endregion

		#region Internal methods
		protected void HttpGet<T>(string endpoint, Action<T> on_success, IReadOnlyDictionary<string, string> parameters = null)
		{
			UriBuilder builder = new UriBuilder(PlayerPrefs.GetString(urlKey));

			builder.Path = string.Format("api/{0}", endpoint);

			if(parameters != null)
			{
				using(FormUrlEncodedContent content = new FormUrlEncodedContent(parameters))
				{
					builder.Query = content.ReadAsStringAsync().Result;
				}
			}
			
			UnityWebRequest request = new UnityWebRequest(builder.Uri);

			request.downloadHandler = new DownloadHandlerBuffer();

			request.SetRequestHeader("Accept", "application/json");
			request.SetRequestHeader("Content-Type", "application/json");

			if(token != null)
			{
				request.SetRequestHeader("Authorization", string.Format("Bearer {0}", token));
			}

			request.SendWebRequest().completed += op =>
			{
				UnityWebRequestAsyncOperation operation = (UnityWebRequestAsyncOperation) op;

				UnityWebRequest req = operation.webRequest;

				if(req.isNetworkError)
				{
					Debug.LogErrorFormat("Error while processing '{0}' request: '{1}'", req.uri, req.error);
				}
				else
				{
					switch(req.responseCode)
					{
						case 200:
							on_success(JsonUtility.FromJson<T>(operation.webRequest.downloadHandler.text));
							break;
						case 401:
							Debug.LogErrorFormat("Unauthorized access to '{0}'", req.uri);
							break;
						default:
							Debug.LogErrorFormat("Failed to access '{0}': status = {1}", req.uri, req.responseCode);
							break;
					}
				}

				req.downloadHandler.Dispose();
				req.Dispose();
			};
		}
		#endregion
	}
}
