﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace LightQueryProfiler.Highlight {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("LightQueryProfiler.Highlight.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot;?&gt;
        ///&lt;definitions&gt;
        ///  &lt;definition name=&quot;ASPX&quot; caseSensitive=&quot;false&quot;&gt;
        ///    &lt;default&gt;
        ///      &lt;font name=&quot;Courier New&quot; size=&quot;11&quot; style=&quot;regular&quot; foreColor=&quot;black&quot; backColor=&quot;transparent&quot; /&gt;
        ///    &lt;/default&gt;
        ///    &lt;pattern name=&quot;ServerSideBlock&quot; type=&quot;block&quot; beginsWith=&quot;&amp;amp;lt;%&quot; endsWith=&quot;%&amp;amp;gt;&quot;&gt;
        ///      &lt;font name=&quot;Courier New&quot; size=&quot;11&quot; style=&quot;regular&quot; foreColor=&quot;black&quot; backColor=&quot;yellow&quot; /&gt;
        ///    &lt;/pattern&gt;
        ///    &lt;pattern name=&quot;Markup&quot; type=&quot;markup&quot; highlightAttributes=&quot;tr [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string DefaultDefinitions {
            get {
                return ResourceManager.GetString("DefaultDefinitions", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;?xml version=&quot;1.0&quot; encoding=&quot;UTF-8&quot;?&gt;
        ///
        ///&lt;xs:schema xmlns:xs=&quot;http://www.w3.org/2001/XMLSchema&quot; elementFormDefault=&quot;qualified&quot;&gt;
        ///  &lt;xs:complexType name=&quot;defaultType&quot;&gt;
        ///    &lt;xs:sequence&gt;
        ///      &lt;xs:element name=&quot;font&quot; type=&quot;fontType&quot; minOccurs=&quot;0&quot; maxOccurs=&quot;1&quot; /&gt;
        ///    &lt;/xs:sequence&gt;
        ///  &lt;/xs:complexType&gt;
        ///
        ///  &lt;xs:complexType name=&quot;styleType&quot;&gt;
        ///    &lt;xs:attribute name=&quot;foreColor&quot; type=&quot;xs:string&quot; use=&quot;required&quot; /&gt;
        ///    &lt;xs:attribute name=&quot;backColor&quot; type=&quot;xs:string&quot; use=&quot;required&quot; /&gt;
        ///  &lt;/xs:complexType&gt;
        ///
        ///  [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string DefinitionsSchema {
            get {
                return ResourceManager.GetString("DefinitionsSchema", resourceCulture);
            }
        }
    }
}
