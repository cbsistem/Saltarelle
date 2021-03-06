using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Saltarelle.Ioc;
#if CLIENT
using System.Html;
using jQueryApi;

#endif

namespace Saltarelle.UI {
	public enum DialogModalityEnum {
		Modeless       = 0,
		Modal          = 1,
		HideOnFocusOut = 2
	}
	
	public abstract class DialogBase : IControl, IClientCreateControl, INotifyCreated
	{
		private const string NoPaddingClassName = "NoPaddingDialog";
		private const short  FirstDialogZIndex  = 10000;
		private const string ModalCoverId       = "DialogModalCoverDiv";
	
		private string id;
		private string title;
		private DialogModalityEnum modality;
		private Position position = PositionHelper.NotPositioned;
		private string className;
		private bool hasPadding;
		private bool removeOnClose;

		#if CLIENT
			private bool hasBgiframe = false;
			private bool isAttached;
			private bool areEventsBound = false;

			public event EventHandler Opened;
			public event CancelEventHandler Closing;
			public event EventHandler Closed;
			public event CancelEventHandler Opening;
			
			private static List<DialogBase> currentShownDialogs = new List<DialogBase>();
		#endif

		public virtual string Id {
			get { return id; }
			set {
				#if CLIENT
					if (isAttached)
						GetElement().ID = value;
				#endif
				id = value;
			}
		}

		public Position Position {
			get { return position; }
			set { position = value; }
		}

		public bool RemoveOnClose {
			get { return removeOnClose; }
			set { removeOnClose = value; }
		}
		
		public DialogModalityEnum Modality {
			get { return modality; }
			set { modality = value; }
		}
		
		public int ModalityInt { // Intended to use in templates.
			get { return (int)modality; }
			set { modality = (DialogModalityEnum)value; }
		}

		public string Title {
			get { return title; }
			set {
				#if CLIENT
					string oldTitle = title;
					title = (value ?? "").Trim();

					if (isAttached) {
						Element elem = GetElement();
						if (string.IsNullOrEmpty(oldTitle) && !string.IsNullOrEmpty(title)) {
							// Add the titlebar.
							var tb = jQuery.FromHtml(TitlebarHtml);
							tb.InsertBefore(jQuery.FromElement(elem.Children[hasBgiframe ? 1 : 0]));
							if (areEventsBound)
								tb.Find("a").Click(evt => { Close(); evt.PreventDefault(); });
						}
						else if (!string.IsNullOrEmpty(oldTitle) && string.IsNullOrEmpty(title)) {
							// Remove the titlebar.
							jQuery.FromElement(elem.Children[hasBgiframe ? 1 : 0]).Remove();
						}
						else if (!string.IsNullOrEmpty(title)) {
							elem.Children[hasBgiframe ? 1 : 0].Children[0].InnerText = title;
						}
					}
				#else
					title = (value ?? "").Trim();
				#endif
			}
		}

		public string ClassName {
			get { return className; }
			set {
				#if CLIENT
					className = (value ?? "").Trim();
					if (isAttached) {
						GetElement().ClassName = EffectiveDialogClass;
					}
				#else
					className = (value ?? "").Trim();
				#endif
			}
		}

		protected abstract string InnerHtml { get; }
		
		private string EffectiveDialogClass {
			get {
				return "ui-dialog ui-widget ui-widget-content ui-corner-all"
				     + (!hasPadding ? " " + NoPaddingClassName : "")
				     + (!string.IsNullOrEmpty(className) ? " " + className : "");
			}
		}
		
		private string TitlebarHtml {
			get {
				if (string.IsNullOrEmpty(title))
					return null;
				return "<div style=\"MozUserSelect: none; *width: expression((this.nextSibling.clientWidth - this.children[1].clientWidth) + 'px')\" class=\"ui-dialog-titlebar ui-widget-header ui-corner-all ui-helper-clearfix\" unselectable=\"on\">"
				     +     "<span style=\"MozUserSelect: none\" class=\"ui-dialog-title\" unselectable=\"on\">" + title + "</span>"
				     +     "<a style=\"MozUserSelect: none\" class=\"ui-dialog-titlebar-close ui-corner-all\" href=\"#\"><span style=\"MozUserSelect: none\" class=\"ui-icon ui-icon-closethick\" unselectable=\"on\">close</span></a>"
				     + "</div>";
			}
		}

		public string Html {
			get {
				if (string.IsNullOrEmpty(id))
					throw new Exception("Must set ID before render");
				return "<div id=\"" + Utils.HtmlEncode(id) + "\" style=\"position: absolute; width: auto\" class=\"" + EffectiveDialogClass + "\" tabindex=\"-1\" unselectable=\"on\">"
				     +    TitlebarHtml
				     +    "<div class=\"ui-dialog-content ui-widget-content\" style=\"*zoom: 1; *float: left; *display: inline-block\">"
				     +        InnerHtml
				     +    "</div>"
				     + "</div>";
			}
		}
		
		public bool HasPadding {
			get { return hasPadding; }
			set {
				hasPadding = value;
				#if CLIENT
					if (isAttached) {
						GetElement().ClassName = EffectiveDialogClass;
					}
				#endif
			}
		}
		
		protected virtual void InitDefault() {
			title         = "";
			className     = "";
			modality      = DialogModalityEnum.Modeless;
			hasPadding    = true;
			removeOnClose = false;
		}

#if SERVER
		protected DialogBase() {
		}
		
		protected virtual void AddItemsToConfigObject(Dictionary<string, object> config) {
			config["id"]            = id;
			config["modality"]      = modality;
			config["title"]         = title;
			config["position"]      = position;
			config["hasPadding"]    = hasPadding;
			config["className"]     = className;
			config["removeOnClose"] = removeOnClose;
		}

		public virtual void DependenciesAvailable() {
			InitDefault();
		}

		public object ConfigObject {
			get {
				var config = new Dictionary<string, object>();
				AddItemsToConfigObject(config);
				return config;
			}
		}
#endif
#if CLIENT
		private static void RepositionCover(Element cover) {
			cover.Style.Top    = Math.Max(Document.Body.ScrollTop,  Document.DocumentElement.ScrollTop).ToString() + "px";
			cover.Style.Left   = Math.Max(Document.Body.ScrollLeft, Document.DocumentElement.ScrollLeft).ToString() + "px";
   			cover.Style.Width  = Document.DocumentElement.ClientWidth.ToString()  + "px";
			cover.Style.Height = Document.DocumentElement.ClientHeight.ToString() + "px";
		}

		private static Element GetModalCover(bool createIfMissing) {
			Element elem = Document.GetElementById(ModalCoverId);
			if (elem != null || !createIfMissing)
				return elem;
			
			var jq = jQuery.FromHtml("<div id=\"" + ModalCoverId + "\" class=\"ui-widget-overlay\" style=\"display: none\">&nbsp;</div>");
			if (jQuery.Browser.MSIE && Utils.ParseDouble(jQuery.Browser.Version) < 7.0) {
				jq.BGIFrame();
				// Need to position the cover in JavaScript. In all other browsers, this is done in CSS.
				jQuery.Window.Scroll(evt => {
					RepositionCover(GetModalCover(false));
				});
				jQuery.Window.Resize(evt =>  {
					RepositionCover(GetModalCover(false));
				});
				RepositionCover(jq.GetElement(0));
			}

			jq.AppendTo(Document.Body);
			return jq.GetElement(0);
		}

		private JsDictionary config;

		[AlternateSignature]
		protected DialogBase() {}

		protected DialogBase(object config) {
			this.config = (!Script.IsUndefined(config) ? JsDictionary.GetDictionary(config) : null);
		}

		public virtual void DependenciesAvailable() {
			if (config != null) {
				InitConfig(JsDictionary.GetDictionary(config));
			}
			else
				InitDefault();
		}
		
		protected virtual void InitConfig(JsDictionary config) {
			id            = (string)config["id"];
			title         = (string)config["title"];
			modality      = (DialogModalityEnum)config["modality"];
			position      = (Position)config["position"];
			hasPadding    = (bool)config["hasPadding"];
			className     = (string)config["className"];
			removeOnClose = (bool)config["removeOnClose"];
			AttachSelf();
		}

		public Element GetElement() { return isAttached ? Document.GetElementById(id) : null; }
		
		private void MoveElementToEnd(Element elem) {
			elem.ParentNode.RemoveChild(elem);
			Document.Body.AppendChild(elem);
		}

		protected virtual void AttachSelf() {
			if (id == null || isAttached)
				throw new Exception("Must set ID and can only attach once");
			isAttached = true;

			// Move the dialog to the end of the body.
			Element element = GetElement();
			MoveElementToEnd(element);
			element.Style.Display = "none";
		}

		public virtual void Attach() {
			AttachSelf();
		}

		public void Open() {
			if (IsOpen)
				Close();
			if (IsOpen)
				return;	// Apparantly a Closing handler prevented us from closing.

			if (!isAttached)
				throw new Exception("Cannot open dialog before attach");

			CancelEventArgs e = new CancelEventArgs();
			OnOpening(e);
			if (e.Cancel)
				return;

			Element elem = GetElement();
			
			if (!areEventsBound) {
				jQuery.FromElement(elem).LostFocus(Element_LostFocus);
				if (!string.IsNullOrEmpty(title)) {
					jQuery.FromElement(elem.Children[0].GetElementsByTagName("a")[0]).Click(evt => { Close(); evt.PreventDefault(); });
				}
				areEventsBound = true;
			}

			// Defer the bgiframe until opening to save load time.
			if (!hasBgiframe && jQuery.Browser.MSIE && Utils.ParseDouble(jQuery.Browser.Version) < 7) {
				jQuery.FromElement(elem).BGIFrame();
				hasBgiframe = true;
			}
			
			short zIndex = FirstDialogZIndex;
			if (currentShownDialogs.Count > 0) {
				var tail = currentShownDialogs[currentShownDialogs.Count - 1];
				zIndex = (short)(tail.GetElement().Style.ZIndex + 2);
			}

			// Move the element to the correct position, or to (0, 0) if it is to be centered later.
			elem.Style.Left    = (position.anchor == AnchoringEnum.TopLeft ? position.left : 0).ToString() + "px";
			elem.Style.Top     = (position.anchor == AnchoringEnum.TopLeft ? position.top  : 0).ToString() + "px";
			elem.Style.ZIndex  = zIndex;
			// Show the dialog.
			elem.Style.Display = "";

			if (position.anchor != AnchoringEnum.TopLeft) {
				// Center the dialog
				var el = jQuery.FromElement(elem);
				elem.Style.Left = Math.Round(Document.Body.ScrollLeft + (jQuery.Window.GetWidth()  - el.GetWidth() ) / 2).ToString() + "px";
				elem.Style.Top  = Math.Round(Document.Body.ScrollTop  + (jQuery.Window.GetHeight() - el.GetHeight()) / 2).ToString() + "px";
			}
			
			if (modality == DialogModalityEnum.Modal) {
				Element cover = GetModalCover(true);
				cover.Style.ZIndex  = (short)(zIndex - 1);
				cover.Style.Display = "";
			}

			currentShownDialogs.Add(this);

			elem.Focus();
			OnOpened(EventArgs.Empty);
		}
		
		public bool IsOpen {
			get { return isAttached ? GetElement().Style.Display != "none" : false; }
		}
		
		public void Close() {
			if (!IsOpen)
				return;
		
			CancelEventArgs e = new CancelEventArgs();
			OnClosing(e);
			if (e.Cancel)
				return;

			// remove this dialog from the shown list
			for (int i = 0; i < currentShownDialogs.Count; i++) {
				if (currentShownDialogs[i] == this) {
					currentShownDialogs.RemoveAt(i);
					break;
				}
			}

			// find the topmost modal dialog
			int modalIndex = -1;
			for (int i = currentShownDialogs.Count - 1; i >= 0; i--) {
				if (((DialogBase)currentShownDialogs[i]).Modality == DialogModalityEnum.Modal) {
					modalIndex = i;
					break;
				}
			}

			// handle the modal cover
			Element cover = GetModalCover(false);
			if (modalIndex == -1) {
				if (cover != null)
					cover.Style.Display = "none";
			}
			else {
				cover.Style.ZIndex = (short)(((DialogBase)currentShownDialogs[modalIndex]).GetElement().Style.ZIndex - 1);
			}

			GetElement().Style.Display = "none";
			if (currentShownDialogs.Count > 0)
				currentShownDialogs[currentShownDialogs.Count - 1].Focus();

			OnClosed(EventArgs.Empty);
		}

		protected virtual void OnOpening(CancelEventArgs e) {
			if (Opening != null)
				Opening(this, e);
		}
		
		protected virtual void OnOpened(EventArgs e) {
			if (Opened != null)
				Opened(this, e);
		}

		protected virtual void OnClosing(CancelEventArgs e) {
			if (Closing != null)
				Closing(this, e);
		}

		protected virtual void OnClosed(EventArgs e) {
			if (Closed != null)
				Closed(this, e);

			if (removeOnClose) {
				jQuery.FromElement(GetElement()).Remove();
				isAttached = false;
			}
		}
		
		private void ModalFocusOut() {
			Element activeElem = Document.ActiveElement;

			bool ok = false;
			int i;
			for (i = 0; i < currentShownDialogs.Count; i++) {
				if (currentShownDialogs[i] == this)
					break;
			}
			if (i < currentShownDialogs.Count) {
				for (i = i + 1; i < currentShownDialogs.Count; i++) {	// allow focus to go to a later dialog
					if (currentShownDialogs[i].GetElement().Contains(activeElem)) {
						ok = true;
						break;
					}
				}
			}
			else
				ok = true;	// the dialog is no longer on the stack - it is being hidden

			if (!ok)
				Focus();
		}
		
		private void VolatileFocusOut() {
			Element activeElem = Document.ActiveElement;

			// find out whether it's a child of ours or of a dialog later in the dialog stack
			int i = 0;
			for (i = 0; i < currentShownDialogs.Count; i++) {
				if (currentShownDialogs[i] == this)
					break;
			}
			for (; i < currentShownDialogs.Count; i++) {
				if (currentShownDialogs[i].GetElement().Contains(activeElem))
					return;
			}
			
			// Focus left us - hide.
			Close();
			if (IsOpen)
				Focus();	// Just in case a Closing handler prevented the close.
		}
		
		private void Element_LostFocus(jQueryEvent evt) {
			switch (modality) {
				case DialogModalityEnum.Modal:
					Window.SetTimeout(ModalFocusOut, 0);
					break;
				case DialogModalityEnum.HideOnFocusOut:
					Window.SetTimeout(VolatileFocusOut, 0);
					break;
			}
		}
		
		public virtual void Focus() {
			GetElement().Focus();
		}
#endif
	}

	public sealed class DialogFrame : DialogBase, IControlHost {
		private string innerHtml;

		public void SetInnerFragments(string[] fragments) {
			#if CLIENT
				if (GetElement() != null)
					throw new Exception("Can't change inner HTML after render.");
			#endif
			innerHtml = Utils.JoinStrings("", fragments ?? new string[0]);
		}

		protected override string InnerHtml { get { return innerHtml ?? ""; } }

#if CLIENT
		[AlternateSignature]
		public DialogFrame() {}

		public DialogFrame(object config) : base(config) {
		}

		public IList<Element> GetInnerElements() {
			var jq = jQuery.FromElement(GetElement());
			var result = new List<Element>();
			for (int i = 0; i < jq.Size(); i++)
				result.Add(jq.GetElement(i));
			return result;
		}
#endif
	}
	
	public abstract class ControlDialogBase : DialogBase {
		private IControl containedControl;
		
		private IContainer container;
		#if SERVER
		[ClientInject]
		#endif
		public IContainer Container { get { return container; } set { container = value; } }

		public override string Id {
			get { return base.Id; }
			set {
				base.Id = value;
				if (containedControl != null)
					containedControl.Id = value + "_control";
			}
		}
		
		protected IControl GetContainedControlBase() { return containedControl; }

#if SERVER
		protected override void AddItemsToConfigObject(Dictionary<string, object> config) {
			base.AddItemsToConfigObject(config);
			config.Add("containedControlType", containedControl.GetType().FullName);
			config.Add("containedControlData", containedControl.ConfigObject);
		}

		protected override string InnerHtml { get { return containedControl.Html; } }

		protected void SetContainedControlBase(IControl value) {
			containedControl = value;
			if (!string.IsNullOrEmpty(Id))
				containedControl.Id = Id + "_control";
		}
#endif
#if CLIENT
		[AlternateSignature]
		protected ControlDialogBase() {}
		protected ControlDialogBase(object config) : base(config) {
		}

		protected override void InitConfig(JsDictionary config) {
			containedControl = (IControl)container.CreateObjectByTypeNameWithConstructorArg((string)config["containedControlType"], config["containedControlData"]);
			base.InitConfig(config);
		}

		protected override string InnerHtml { get { return ((IClientCreateControl)containedControl).Html; } }
		
		protected void SetContainedControlBase(IClientCreateControl control) {
			if (control.GetElement() != null)
				throw new Exception("The control must not be rendered.");
			containedControl = (IControl)control;
			if (!string.IsNullOrEmpty(Id))
				((IControl)control).Id = Id + "_control";
		}
		
		public override void Attach() {
			((IClientCreateControl)containedControl).Attach();
			AttachSelf();
		}
#endif
	}
	
	public sealed class ControlDialog : ControlDialogBase {
		public IControl GetContainedControl() {
			return GetContainedControlBase();
		}

#if SERVER
		public void SetContainedControl(IControl value) {
			SetContainedControlBase(value);
		}
#endif
#if CLIENT
		[AlternateSignature]
		public ControlDialog() {}
		
		public ControlDialog(object config) : base(config) {
		}

		public void SetContainedControl(IClientCreateControl control) {
			SetContainedControlBase(control);
		}
#endif
	}
}
