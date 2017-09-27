﻿using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Foundation;
using WebKit;
using Xam.Plugin.Abstractions;
using Xam.Plugin.Abstractions.Enumerations;
using Xam.Plugin.MacOS;
using Xamarin.Forms.Platform.MacOS;

[assembly: Xamarin.Forms.ExportRenderer(typeof(FormsWebView), typeof(FormsWebViewRenderer))]
namespace Xam.Plugin.MacOS
{
    public class FormsWebViewRenderer : ViewRenderer<FormsWebView, WKWebView>, IWKScriptMessageHandler, IWKUIDelegate
    {

		public event EventHandler<WKWebView> OnControlChanged;

		public static string BaseUrl { get; set; } = NSBundle.MainBundle.BundlePath;

		FormsNavigationDelegate _navigationDelegate;

		WKWebViewConfiguration _configuration;

		WKUserContentController _contentController;

		public static void Initialize()
		{
			var dt = DateTime.Now;
		}

		protected override void OnElementChanged(ElementChangedEventArgs<FormsWebView> e)
		{
			base.OnElementChanged(e);

			if (Control == null)
				SetupControl();

			if (e.NewElement != null)
				SetupElement(e.NewElement);

			if (e.OldElement != null)
				DestroyElement(e.OldElement);
		}

		void SetupElement(FormsWebView element)
		{
			element.PropertyChanged += OnPropertyChanged;
			element.OnJavascriptInjectionRequest += OnJavascriptInjectionRequest;
			element.OnBackRequested += OnBackRequested;
			element.OnForwardRequested += OnForwardRequested;
			element.OnRefreshRequested += OnRefreshRequested;

			SetSource();
		}

		void DestroyElement(FormsWebView element)
		{
			element.PropertyChanged -= OnPropertyChanged;
			element.OnJavascriptInjectionRequest -= OnJavascriptInjectionRequest;
			element.OnBackRequested -= OnBackRequested;
			element.OnForwardRequested -= OnForwardRequested;
			element.OnRefreshRequested -= OnRefreshRequested;

			element.Dispose();
		}

		void SetupControl()
		{
			_navigationDelegate = new FormsNavigationDelegate(this);
			_contentController = new WKUserContentController();
			_contentController.AddScriptMessageHandler(this, "invokeAction");
			_configuration = new WKWebViewConfiguration
			{
				UserContentController = _contentController
			};

			var wkWebView = new WKWebView(Frame, _configuration)
			{
				UIDelegate = this,
				NavigationDelegate = _navigationDelegate
			};

			FormsWebView.CallbackAdded += OnCallbackAdded;

			SetNativeControl(wkWebView);
			OnControlChanged?.Invoke(this, wkWebView);
		}

		async void OnCallbackAdded(object sender, string e)
		{
			if (string.IsNullOrWhiteSpace(e)) return;
			await OnJavascriptInjectionRequest(FormsWebView.GenerateFunctionScript(e));
		}

		void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case "Source":
					SetSource();
					break;
			}
		}

		internal async Task<string> OnJavascriptInjectionRequest(string js)
		{
			if (Control == null || Element == null) return string.Empty;

			var response = string.Empty;
			var obj = await Control.EvaluateJavaScriptAsync(js);

			if (obj != null)
				response = obj.ToString();

			return response;
		}

		void SetSource()
		{
			if (Element == null || Control == null) return;

			switch (Element.ContentType)
			{
				case WebViewContentType.Internet:
					LoadInternetContent();
					break;

				case WebViewContentType.LocalFile:
					LoadLocalFile();
					break;

				case WebViewContentType.StringData:
					LoadStringData();
					break;
			}
		}

		void LoadStringData()
		{
			if (Control == null || Element == null) return;

			var nsBaseUri = new NSUrl($"file://{Element.BaseUrl ?? BaseUrl}");
			Control.LoadHtmlString(Element.Source, nsBaseUri);
		}

		void LoadLocalFile()
		{
			if (Control == null || Element == null) return;

			var path = Path.Combine(Element.BaseUrl ?? BaseUrl, Element.Source);
			var nsFileUri = new NSUrl($"file://{path}");
			var nsBaseUri = new NSUrl($"file://{Element.BaseUrl ?? BaseUrl}");

			Control.LoadFileUrl(nsFileUri, nsBaseUri);
		}

		void LoadInternetContent()
		{
			if (Control == null || Element == null) return;

			var headers = new NSMutableDictionary();

			foreach (var header in FormsWebView.GlobalRegisteredHeaders)
				headers.Add(new NSString(header.Key), new NSString(header.Value));

			foreach (var header in Element.LocalRegisteredHeaders)
				headers.Add(new NSString(header.Key), new NSString(header.Value));

			var url = new NSUrl(Element.Source);
			var request = new NSMutableUrlRequest(url)
			{
				Headers = headers
			};

			Control.LoadRequest(request);
		}

		public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
		{
			if (Element == null || message == null || message.Body == null) return;
			Element.HandleScriptReceived(message.Body.ToString());
		}

		void OnRefreshRequested(object sender, EventArgs e)
		{
			if (Control == null) return;
			Control.ReloadFromOrigin();
		}

		void OnForwardRequested(object sender, EventArgs e)
		{
			if (Control == null || Element == null) return;

			if (Control.CanGoForward)
				Control.GoForward();
		}

		void OnBackRequested(object sender, EventArgs e)
		{
			if (Control == null || Element == null) return;

			if (Control.CanGoBack)
				Control.GoBack();
		}
    }
}
