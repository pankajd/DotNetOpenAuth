//-----------------------------------------------------------------------
// <copyright file="InfoCardSelector.cs" company="Andrew Arnott">
//     Copyright (c) Andrew Arnott. All rights reserved.
//     Certain elements are Copyright (c) 2007 Dominick Baier.
// </copyright>
//-----------------------------------------------------------------------

[assembly: System.Web.UI.WebResource("DotNetOpenAuth.InfoCard.SupportingScript.js", "text/javascript")]

namespace DotNetOpenAuth.InfoCard {
	using System;
	using System.Collections.ObjectModel;
	using System.ComponentModel;
	using System.Diagnostics.Contracts;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Web.UI;
	using System.Web.UI.HtmlControls;
	using System.Web.UI.WebControls;
	using System.Xml.XPath;
	using DotNetOpenAuth.Messaging;

	/// <summary>
	/// The type of Information Card that is being asked for.
	/// </summary>
	public enum IssuerType {
		/// <summary>
		/// An InfoCard that the user creates at his/her own machine.
		/// </summary>
		SelfIssued,

		/// <summary>
		/// An InfoCard that is supplied by a third party so assert claims as valid
		/// according to that third party.
		/// </summary>
		Managed,
	}

	/// <summary>
	/// The style to use for NOT displaying a hidden region.
	/// </summary>
	public enum RenderMode {
		/// <summary>
		/// A hidden region should be invisible while still occupying space in the page layout.
		/// </summary>
		Static,

		/// <summary>
		/// A hidden region should collapse so that it does not occupy space in the page layout.
		/// </summary>
		Dynamic
	}

	/// <summary>
	/// An Information Card selector ASP.NET control.
	/// </summary>
	[ParseChildren(true, "ClaimTypes")]
	[PersistChildren(false)]
	[DefaultEvent("ReceivedToken")]
	[ToolboxData("<{0}:InfoCardSelector runat=\"server\"><ClaimTypes><{0}:ClaimType Name=\"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/privatepersonalidentifier\" /></ClaimTypes><UnsupportedTemplate><p>Your browser does not support Information Cards.</p></UnsupportedTemplate></{0}:InfoCardSelector>")]
	[ContractVerification(true)]
	public class InfoCardSelector : CompositeControl, IPostBackEventHandler {
		#region Property constants

		/// <summary>
		/// Default value for the <see cref="RenderMode"/> property.
		/// </summary>
		private const RenderMode RenderModeDefault = RenderMode.Dynamic;

		/// <summary>
		/// Default value for the <see cref="AutoPostBack"/> property.
		/// </summary>
		private const bool AutoPostBackDefault = true;

		/// <summary>
		/// Default value for the <see cref="AutoPopup"/> property.
		/// </summary>
		private const bool AutoPopupDefault = false;

		/// <summary>
		/// Default value for the <see cref="PrivacyUrl"/> property.
		/// </summary>
		private const string PrivacyUrlDefault = "";

		/// <summary>
		/// Default value for the <see cref="PrivacyVersion"/> property.
		/// </summary>
		private const string PrivacyVersionDefault = "";

		/// <summary>
		/// Default value for the <see cref="InfoCardImage"/> property.
		/// </summary>
		private const InfoCardImageSize InfoCardImageDefault = InfoCardImage.DefaultImageSize;

		/// <summary>
		/// Default value for the <see cref="IssuerPolicy"/> property.
		/// </summary>
		private const string IssuerPolicyDefault = "";

		/// <summary>
		/// Default value for the <see cref="Issuer"/> property.
		/// </summary>
		private const string IssuerDefault = "";

		/// <summary>
		/// The default value for the <see cref="IssuerType"/> property.
		/// </summary>
		private const IssuerType IssuerTypeDefault = IssuerType.SelfIssued;

		/// <summary>
		/// The default value for the <see cref="TokenType"/> property.
		/// </summary>
		private const string TokenTypeDefault = "urn:oasis:names:tc:SAML:1.0:assertion";

		/// <summary>
		/// The viewstate key for storing the <see cref="Issuer" /> property.
		/// </summary>
		private const string IssuerViewStateKey = "Issuer";

		/// <summary>
		/// The viewstate key for storing the <see cref="IssuerPolicy" /> property.
		/// </summary>
		private const string IssuerPolicyViewStateKey = "IssuerPolicy";

		/// <summary>
		/// The viewstate key for storing the <see cref="AutoPopup" /> property.
		/// </summary>
		private const string AutoPopupViewStateKey = "AutoPopup";

		/// <summary>
		/// The viewstate key for storing the <see cref="ClaimTypes" /> property.
		/// </summary>
		private const string ClaimTypesViewStateKey = "ClaimTypes";

		/// <summary>
		/// The viewstate key for storing the <see cref="TokenType" /> property.
		/// </summary>
		private const string TokenTypeViewStateKey = "TokenType";

		/// <summary>
		/// The viewstate key for storing the <see cref="PrivacyUrl" /> property.
		/// </summary>
		private const string PrivacyUrlViewStateKey = "PrivacyUrl";

		/// <summary>
		/// The viewstate key for storing the <see cref="PrivacyVersion" /> property.
		/// </summary>
		private const string PrivacyVersionViewStateKey = "PrivacyVersion";

		/// <summary>
		/// The viewstate key for storing the <see cref="AutoPostBack" /> property.
		/// </summary>
		private const string AutoPostBackViewStateKey = "AutoPostBack";

		/// <summary>
		/// The viewstate key for storing the <see cref="IssuerType" /> property.
		/// </summary>
		private const string IssuerTypeViewStateKey = "IssueType";

		/// <summary>
		/// The viewstate key for storing the <see cref="ImageSize" /> property.
		/// </summary>
		private const string ImageSizeViewStateKey = "ImageSize";

		/// <summary>
		/// The viewstate key for storing the <see cref="RenderMode" /> property.
		/// </summary>
		private const string RenderModeViewStateKey = "RenderMode";

		#endregion

		#region Categories

		/// <summary>
		/// The "Behavior" property category.
		/// </summary>
		private const string BehaviorCategory = "Behavior";

		/// <summary>
		/// The "Appearance" property category.
		/// </summary>
		private const string AppearanceCategory = "Appearance";

		/// <summary>
		/// The "InfoCard" property category.
		/// </summary>
		private const string InfoCardCategory = "InfoCard";

		#endregion

		/// <summary>
		/// The Issuer URI to use for self-issued cards.
		/// </summary>
		private const string SelfIssuedUri = "http://schemas.xmlsoap.org/ws/2005/05/identity/issuer/self";

		/// <summary>
		/// The resource name for getting at the SupportingScript.js embedded manifest stream.
		/// </summary>
		private const string ScriptResourceName = "DotNetOpenAuth.InfoCard.SupportingScript.js";

		/// <summary>
		/// The panel containing the controls to display if InfoCard is supported in the user agent.
		/// </summary>
		private Panel infoCardSupportedPanel;

		/// <summary>
		/// The panel containing the controls to display if InfoCard is NOT supported in the user agent.
		/// </summary>
		private Panel infoCardNotSupportedPanel;

		/// <summary>
		/// Occurs when an InfoCard has been submitted but not decoded yet.
		/// </summary>
		[Category(InfoCardCategory)]
		public event EventHandler<ReceivingTokenEventArgs> ReceivingToken;

		/// <summary>
		/// Occurs when an InfoCard has been submitted and decoded.
		/// </summary>
		[Category(InfoCardCategory)]
		public event EventHandler<ReceivedTokenEventArgs> ReceivedToken;

		/// <summary>
		/// Occurs when an InfoCard token is submitted but an error occurs in processing.
		/// </summary>
		[Category(InfoCardCategory)]
		public event EventHandler<TokenProcessingErrorEventArgs> TokenProcessingError;

		#region Properties

		/// <summary>
		/// Gets the set of claims that are requested from the Information Card..
		/// </summary>
		[Description("Specifies the required and optional claims.")]
		[PersistenceMode(PersistenceMode.InnerProperty), Category(InfoCardCategory)]
		public Collection<ClaimType> ClaimTypes {
			get {
				Contract.Ensures(Contract.Result<Collection<ClaimType>>() != null);
				if (this.ViewState[ClaimTypesViewStateKey] == null) {
					var claims = new Collection<ClaimType>();
					this.ViewState[ClaimTypesViewStateKey] = claims;
					return claims;
				} else {
					return (Collection<ClaimType>)this.ViewState[ClaimTypesViewStateKey];
				}
			}
		}

		/// <summary>
		/// Gets or sets the issuer URI, applicable only if the <see cref="IssuerType"/>
		/// property is set to <see cref="InfoCard.IssuerType.Managed"/>.
		/// </summary>
		[Description("When receiving managed cards, this is the only Issuer whose cards will be accepted.")]
		[Category(InfoCardCategory), DefaultValue(IssuerDefault)]
		public string Issuer {
			get { return (string)this.ViewState[IssuerViewStateKey] ?? IssuerDefault; }
			set { this.ViewState[IssuerViewStateKey] = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating whether a self-issued or a managed Card should be submitted.
		/// </summary>
		[Description("Specifies the issuer type. Select Managed to specify the issuer URI on your own. Select SelfIssued to use the well-known self issued URI.")]
		[Category(InfoCardCategory), DefaultValue(IssuerTypeDefault)]
		public IssuerType IssuerType {
			get { return (IssuerType)(this.ViewState[IssuerTypeViewStateKey] ?? IssuerTypeDefault); }
			set { this.ViewState[IssuerTypeViewStateKey] = value; }
		}

		/// <summary>
		/// Gets or sets the issuer policy URI.
		/// </summary>
		[Description("Specifies the URI of the issuer MEX endpoint")]
		[Category(InfoCardCategory), DefaultValue(IssuerPolicyDefault)]
		public string IssuerPolicy {
			get { return (string)this.ViewState[IssuerPolicyViewStateKey] ?? IssuerPolicyDefault; }
			set { this.ViewState[IssuerPolicyViewStateKey] = value; }
		}

		/// <summary>
		/// Gets or sets the URL to this site's privacy policy.
		/// </summary>
		[Description("The URL to this site's privacy policy.")]
		[Category(InfoCardCategory), DefaultValue(PrivacyUrlDefault)]
		public string PrivacyUrl {
			get { return (string)this.ViewState[PrivacyUrlViewStateKey] ?? PrivacyUrlDefault; }
			set { this.ViewState[PrivacyUrlViewStateKey] = value; }
		}

		/// <summary>
		/// Gets or sets the version of the privacy policy file.
		/// </summary>
		[Description("Specifies the version of the privacy policy file")]
		[Category(InfoCardCategory), DefaultValue(PrivacyVersionDefault)]
		public string PrivacyVersion {
			get { return (string)this.ViewState[PrivacyVersionViewStateKey] ?? PrivacyVersionDefault; }
			set { this.ViewState[PrivacyVersionViewStateKey] = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating whether a postback will automatically
		/// be invoked when the user selects an Information Card.
		/// </summary>
		[Description("Specifies if the pages automatically posts back after the user has selected a card")]
		[Category(BehaviorCategory), DefaultValue(AutoPostBackDefault)]
		public bool AutoPostBack {
			get { return (bool)(this.ViewState[AutoPostBackViewStateKey] ?? AutoPostBackDefault); }
			set { this.ViewState[AutoPostBackViewStateKey] = value; }
		}

		/// <summary>
		/// Gets or sets the size of the standard InfoCard image to display.
		/// </summary>
		/// <value>The default size is 114x80.</value>
		[Description("The size of the InfoCard image to use. Defaults to 114x80.")]
		[DefaultValue(InfoCardImageDefault), Category(AppearanceCategory)]
		public InfoCardImageSize ImageSize {
			get { return (InfoCardImageSize)(this.ViewState[ImageSizeViewStateKey] ?? InfoCardImageDefault); }
			set { this.ViewState[ImageSizeViewStateKey] = value; }
		}

		/// <summary>
		/// Gets or sets the template to display when the user agent lacks
		/// an Information Card selector.
		/// </summary>
		[Browsable(false), DefaultValue("")]
		[PersistenceMode(PersistenceMode.InnerProperty), TemplateContainer(typeof(InfoCardSelector))]
		public virtual ITemplate UnsupportedTemplate { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether a hidden region (either
		/// the unsupported or supported InfoCard HTML)
		/// collapses or merely becomes invisible when it is not to be displayed.
		/// </summary>
		[Description("Whether the hidden region collapses or merely becomes invisible.")]
		[Category(AppearanceCategory), DefaultValue(RenderModeDefault)]
		public RenderMode RenderMode {
			get { return (RenderMode)(this.ViewState[RenderModeViewStateKey] ?? RenderModeDefault); }
			set { this.ViewState[RenderModeViewStateKey] = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating whether the identity selector will be triggered at page load.
		/// </summary>
		[Description("Controls whether the InfoCard selector automatically appears when the page is loaded.")]
		[Category(BehaviorCategory), DefaultValue(AutoPopupDefault)]
		public bool AutoPopup {
			get { return (bool)(this.ViewState[AutoPopupViewStateKey] ?? AutoPopupDefault); }
			set { this.ViewState[AutoPopupViewStateKey] = value; }
		}

		#endregion

		/// <summary>
		/// Gets the name of the hidden field that is used to transport the token back to the server.
		/// </summary>
		private string HiddenFieldName {
			get { return this.ClientID + "_tokenxml"; }
		}

		/// <summary>
		/// Gets the XML token, which will be encrypted if it was received over SSL.
		/// </summary>
		private string TokenXml {
			get { return this.Page.Request.Form[this.HiddenFieldName]; }
		}

		/// <summary>
		/// Gets or sets the type of token the page is prepared to receive.
		/// </summary>
		[Description("Specifies the token type. Defaults to SAML 1.0")]
		[DefaultValue(TokenTypeDefault), Category(InfoCardCategory)]
		private string TokenType {
			get { return (string)this.ViewState[TokenTypeViewStateKey] ?? TokenTypeDefault; }
			set { this.ViewState[TokenTypeViewStateKey] = value; }
		}

		/// <summary>
		/// When implemented by a class, enables a server control to process an event raised when a form is posted to the server.
		/// </summary>
		/// <param name="eventArgument">A <see cref="T:System.String"/> that represents an optional event argument to be passed to the event handler.</param>
		public void RaisePostBackEvent(string eventArgument) {
			if (!string.IsNullOrEmpty(this.TokenXml)) {
				bool encrypted = Token.IsEncrypted(this.TokenXml);
				TokenDecryptor decryptor = encrypted ? new TokenDecryptor() : null;
				ReceivingTokenEventArgs receivingArgs = this.OnReceivingToken(this.TokenXml, decryptor);

				if (!receivingArgs.Cancel) {
					try {
						Token token = new Token(this.TokenXml, this.Page.Request.Url, decryptor);
						this.OnReceivedToken(token);
					} catch (InformationCardException ex) {
						this.OnTokenProcessingError(this.TokenXml, ex);
						return;
					}
				}
			}
		}

		/// <summary>
		/// Fires the <see cref="ReceivingToken"/> event.
		/// </summary>
		/// <param name="tokenXml">The token XML, prior to any processing.</param>
		/// <param name="decryptor">The decryptor to use, if the token is encrypted.</param>
		/// <returns>The event arguments sent to the event handlers.</returns>
		protected virtual ReceivingTokenEventArgs OnReceivingToken(string tokenXml, TokenDecryptor decryptor) {
			Contract.Requires(tokenXml != null);
			ErrorUtilities.VerifyArgumentNotNull(tokenXml, "tokenXml");

			var args = new ReceivingTokenEventArgs(tokenXml, decryptor);
			var receivingToken = this.ReceivingToken;
			if (receivingToken != null) {
				receivingToken(this, args);
			}

			return args;
		}

		/// <summary>
		/// Fires the <see cref="ReceivedToken"/> event.
		/// </summary>
		/// <param name="token">The token, if it was decrypted.</param>
		protected virtual void OnReceivedToken(Token token) {
			Contract.Requires(token != null);
			ErrorUtilities.VerifyArgumentNotNull(token, "token");

			var receivedInfoCard = this.ReceivedToken;
			if (receivedInfoCard != null) {
				receivedInfoCard(this, new ReceivedTokenEventArgs(token));
			}
		}

		/// <summary>
		/// Fires the <see cref="TokenProcessingError"/> event.
		/// </summary>
		/// <param name="unprocessedToken">The unprocessed token.</param>
		/// <param name="ex">The exception generated while processing the token.</param>
		protected virtual void OnTokenProcessingError(string unprocessedToken, Exception ex) {
			Contract.Requires(unprocessedToken != null);
			Contract.Requires(ex != null);

			var tokenProcessingError = this.TokenProcessingError;
			if (tokenProcessingError != null) {
				TokenProcessingErrorEventArgs args = new TokenProcessingErrorEventArgs(unprocessedToken, ex);
				tokenProcessingError(this, args);
			}
		}

		/// <summary>
		/// Raises the <see cref="E:System.Web.UI.Control.Init"/> event.
		/// </summary>
		/// <param name="e">An <see cref="T:System.EventArgs"/> object that contains the event data.</param>
		protected override void OnInit(EventArgs e) {
			base.OnInit(e);
			this.Page.LoadComplete += delegate { this.EnsureChildControls(); };
		}

		/// <summary>
		/// Called by the ASP.NET page framework to notify server controls that use composition-based implementation to create any child controls they contain in preparation for posting back or rendering.
		/// </summary>
		protected override void CreateChildControls() {
			base.CreateChildControls();

			this.Page.ClientScript.RegisterHiddenField(this.HiddenFieldName, "");

			this.Controls.Add(this.infoCardSupportedPanel = this.CreateInfoCardSupportedPanel());
			this.Controls.Add(this.infoCardNotSupportedPanel = this.CreateInfoCardUnsupportedPanel());

			this.RenderSupportingScript();
		}

		/// <summary>
		/// Creates a control that renders to &lt;Param Name="{0}" Value="{1}" /&gt;
		/// </summary>
		/// <param name="name">The parameter name.</param>
		/// <param name="value">The parameter value.</param>
		/// <returns>The control that renders to the Param tag.</returns>
		private static Control CreateParam(string name, string value) {
			Contract.Ensures(Contract.Result<Control>() != null);
			HtmlGenericControl control = new HtmlGenericControl(HtmlTextWriterTag.Param.ToString());
			control.Attributes.Add(HtmlTextWriterAttribute.Name.ToString(), name);
			control.Attributes.Add(HtmlTextWriterAttribute.Value.ToString(), value);
			return control;
		}

		/// <summary>
		/// Creates the panel whose contents are displayed to the user
		/// on a user agent that has an Information Card selector.
		/// </summary>
		/// <returns>The Panel control</returns>
		[Pure]
		private Panel CreateInfoCardSupportedPanel() {
			Contract.Ensures(Contract.Result<Panel>() != null);

			Panel supportedPanel = new Panel();

			if (!this.DesignMode) {
				// At the user agent, assume InfoCard is not supported until
				// the JavaScript discovers otherwise and reveals this panel.
				supportedPanel.Style[HtmlTextWriterStyle.Display] = "none";
			}

			supportedPanel.Controls.Add(this.CreateInfoCardSelectorObject());

			// add clickable image
			Image image = new Image();
			image.ImageUrl = this.Page.ClientScript.GetWebResourceUrl(typeof(InfoCardSelector), InfoCardImage.GetImageManifestResourceStreamName(this.ImageSize));
			image.AlternateText = InfoCardStrings.SelectorClickPrompt;
			image.ToolTip = InfoCardStrings.SelectorClickPrompt;
			image.Style[HtmlTextWriterStyle.Cursor] = "hand";

			// generate call do __doPostback
			PostBackOptions options = new PostBackOptions(this);
			string postback = this.Page.ClientScript.GetPostBackEventReference(options);

			// generate the onclick script for the image
			string invokeScript = string.Format(
				CultureInfo.InvariantCulture,
				@"document.getElementById('{0}').value = document.getElementById('{1}_cs').value; {2}",
				this.HiddenFieldName,
				this.ClientID,
				this.AutoPostBack ? postback : "");

			image.Attributes["onclick"] = invokeScript;
			supportedPanel.Controls.Add(image);

			// trigger the selector at page load?
			if (this.AutoPopup && !this.Page.IsPostBack) {
				string loadScript = string.Format(
					CultureInfo.InvariantCulture,
					@"document.getElementById('{0}').value = document.getElementById('{1}_cs').value; {2}",
					this.HiddenFieldName,
					this.ClientID,
					postback);

				this.Page.ClientScript.RegisterStartupScript(typeof(InfoCardSelector), "selector_load_trigger", loadScript, true);
			}

			return supportedPanel;
		}

		/// <summary>
		/// Creates the panel whose contents are displayed to the user
		/// on a user agent that does not have an Information Card selector.
		/// </summary>
		/// <returns>The Panel control.</returns>
		[Pure]
		private Panel CreateInfoCardUnsupportedPanel() {
			Contract.Ensures(Contract.Result<Panel>() != null);

			Panel unsupportedPanel = new Panel();
			if (this.UnsupportedTemplate != null) {
				this.UnsupportedTemplate.InstantiateIn(unsupportedPanel);
			}
			return unsupportedPanel;
		}

		/// <summary>
		/// Creates the info card selector &lt;object&gt; HTML tag.
		/// </summary>
		/// <returns>A control that renders to the &lt;object&gt; tag.</returns>
		[Pure]
		private Control CreateInfoCardSelectorObject() {
			HtmlGenericControl cardSpaceControl = new HtmlGenericControl(HtmlTextWriterTag.Object.ToString());
			cardSpaceControl.Attributes.Add(HtmlTextWriterAttribute.Type.ToString(), "application/x-informationcard");
			cardSpaceControl.Attributes.Add(HtmlTextWriterAttribute.Name.ToString(), this.ClientID + "_cs");
			cardSpaceControl.Attributes.Add(HtmlTextWriterAttribute.Id.ToString(), this.ClientID + "_cs");

			// issuer
			if (this.IssuerType == IssuerType.SelfIssued) {
				cardSpaceControl.Controls.Add(CreateParam("issuer", SelfIssuedUri));
			} else if (IssuerType == IssuerType.Managed) {
				if (!string.IsNullOrEmpty(this.Issuer)) {
					cardSpaceControl.Controls.Add(CreateParam("issuer", this.Issuer));
				}
			}

			// issuer policy
			if (!string.IsNullOrEmpty(this.IssuerPolicy)) {
				cardSpaceControl.Controls.Add(CreateParam("issuerPolicy", this.IssuerPolicy));
			}

			// token type
			if (!string.IsNullOrEmpty(this.TokenType)) {
				cardSpaceControl.Controls.Add(CreateParam("tokenType", this.TokenType));
			}

			// claims
			string requiredClaims, optionalClaims;
			this.GetRequestedClaims(out requiredClaims, out optionalClaims);
			ErrorUtilities.VerifyArgument(!string.IsNullOrEmpty(requiredClaims) || !string.IsNullOrEmpty(optionalClaims), InfoCardStrings.EmptyClaimListNotAllowed);
			if (!string.IsNullOrEmpty(requiredClaims)) {
				cardSpaceControl.Controls.Add(CreateParam("requiredClaims", requiredClaims));
			}
			if (!string.IsNullOrEmpty(optionalClaims)) {
				cardSpaceControl.Controls.Add(CreateParam("optionalClaims", optionalClaims));
			}

			// privacy URL
			if (!string.IsNullOrEmpty(this.PrivacyUrl)) {
				cardSpaceControl.Controls.Add(CreateParam("privacyUrl", this.PrivacyUrl));
			}

			// privacy version
			if (!string.IsNullOrEmpty(this.PrivacyVersion)) {
				cardSpaceControl.Controls.Add(CreateParam("privacyVersion", this.PrivacyVersion));
			}

			return cardSpaceControl;
		}

		/// <summary>
		/// Compiles lists of requested/required claims that should accompany
		/// any submitted Information Card.
		/// </summary>
		/// <param name="required">A space-delimited list of claim type URIs for claims that must be included in a submitted Information Card.</param>
		/// <param name="optional">A space-delimited list of claim type URIs for claims that may optionally be included in a submitted Information Card.</param>
		[Pure]
		private void GetRequestedClaims(out string required, out string optional) {
			Contract.Requires(this.ClaimTypes != null);
			Contract.Ensures(Contract.ValueAtReturn<string>(out required) != null);
			Contract.Ensures(Contract.ValueAtReturn<string>(out optional) != null);

			var nonEmptyClaimTypes = this.ClaimTypes.Where(c => c.Name != null);

			var optionalClaims = from claim in nonEmptyClaimTypes
								 where claim.IsOptional
								 select claim.Name.AbsoluteUri;
			var requiredClaims = from claim in nonEmptyClaimTypes
								 where !claim.IsOptional
								 select claim.Name.AbsoluteUri;

			string[] requiredClaimsArray = requiredClaims.ToArray();
			string[] optionalClaimsArray = optionalClaims.ToArray();
			Contract.Assume(requiredClaimsArray != null);
			Contract.Assume(optionalClaimsArray != null);
			required = string.Join(" ", requiredClaimsArray);
			optional = string.Join(" ", optionalClaimsArray);
		}

		/// <summary>
		/// Adds Javascript snippets to the page to help the Information Card selector do its work,
		/// or to downgrade gracefully if the user agent lacks an Information Card selector.
		/// </summary>
		private void RenderSupportingScript() {
			Contract.Requires(this.infoCardSupportedPanel != null);

			this.Page.ClientScript.RegisterClientScriptResource(typeof(InfoCardSelector), ScriptResourceName);

			if (this.RenderMode == RenderMode.Static) {
				this.Page.ClientScript.RegisterStartupScript(
					typeof(InfoCardSelector),
					"SelectorSupportingScript_" + this.ClientID,
					string.Format(CultureInfo.InvariantCulture, "CheckStatic('{0}', '{1}');", this.infoCardSupportedPanel.ClientID, this.infoCardNotSupportedPanel.ClientID),
					true);
			} else if (RenderMode == RenderMode.Dynamic) {
				this.Page.ClientScript.RegisterStartupScript(
					typeof(InfoCardSelector),
					"SelectorSupportingScript_" + this.ClientID,
					string.Format(CultureInfo.InvariantCulture, "CheckDynamic('{0}', '{1}');", this.infoCardSupportedPanel.ClientID, this.infoCardNotSupportedPanel.ClientID),
					true);
			}
		}
	}
}