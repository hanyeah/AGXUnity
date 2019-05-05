﻿using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;

using Object = UnityEngine.Object;

namespace AGXUnityEditor
{
  public class InspectorEditor : Editor
  {
    public static GUISkin Skin = null;

    public static void DrawMembersGUI( Object[] objects )
    {
      objects = objects.Where( obj => obj != null ).ToArray();

      if ( objects.Length == 0 )
        return;

      Undo.RecordObjects( objects, "Inspector" );

      var hasChanges = false;
      InvokeWrapper[] fieldsAndProperties = InvokeWrapper.FindFieldsAndProperties( objects.FirstOrDefault( obj => obj != null ) );
      foreach ( InvokeWrapper wrapper in fieldsAndProperties ) {
        if ( !ShouldBeShownInInspector( wrapper.Member ) )
          continue;

        hasChanges = HandleType( wrapper, objects ) || hasChanges;
      }

      if ( hasChanges ) {
        foreach ( var obj in objects )
          EditorUtility.SetDirty( obj );
      }
    }

    public int NumTargets { get { return this.targets.Length; } }

    public bool IsMultiSelect { get { return NumTargets > 1; } }

    public void DrawMembersGUI<T>( Func<T, Object> func )
      where T : Object
    {
      DrawMembersGUI( Targets( func ).ToArray() );
    }

    public IEnumerable<T> Targets<T>()
      where T : Object
    {
      foreach ( var obj in this.targets )
        yield return obj as T;
    }

    public IEnumerable<U> Targets<T, U>( Func<T, U> func )
      where U : Object
      where T : Object
    {
      foreach ( var obj in Targets<T>() )
        yield return func( obj );
    }

    public sealed override void OnInspectorGUI()
    {
      ToolManager.OnPreTargetMembers( this.target, this );

      DrawMembersGUI( this.targets );

      ToolManager.OnPostTargetMembers( this.target, this );
    }

    private void OnEnable()
    {
      if ( Skin == null ) {
        Skin = EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector );
        Skin.label.richText = true;
        Skin.toggle.richText = true;
        Skin.button.richText = true;
        Skin.textArea.richText = true;
        Skin.textField.richText = true;

        if ( EditorGUIUtility.isProSkin )
          Skin.label.normal.textColor = 204.0f / 255.0f * Color.white;
      }

      // Entire class/component marked as hidden - enable "hide in inspector".
      if ( this.target.GetType().GetCustomAttributes( typeof( HideInInspector ), false ).Length > 0 )
        this.target.hideFlags |= HideFlags.HideInInspector;

      ToolManager.OnTargetEditorEnable( this.target );
    }

    private void OnDisable()
    {
      ToolManager.OnTargetEditorDisable( this.target );
    }

    private static bool HandleType( InvokeWrapper wrapper, Object[] objects )
    {
      if ( !wrapper.CanRead() )
        return false;

      var drawerInfo = InspectorGUI.GetDrawerMethod( wrapper.GetContainingType() );
      if ( !drawerInfo.IsValid )
        return false;

      EditorGUI.showMixedValue = !wrapper.AreValuesEqual( objects );

      var value   = drawerInfo.Drawer.Invoke( null, new object[] { wrapper, Skin } );
      var changed = UnityEngine.GUI.changed &&
                    ( drawerInfo.IsNullable || value != null );

      EditorGUI.showMixedValue = false;

      if ( !changed )
        return false;

      foreach ( var obj in objects ) {
        object newValue = value;
        if ( drawerInfo.CopyOp != null ) {
          newValue = wrapper.GetValue( obj );
          drawerInfo.CopyOp.Invoke( null, new object[] { value, newValue } );
        }
        wrapper.ConditionalSet( obj, newValue );
      }

      return true;
    }

    private static bool ShouldBeShownInInspector( MemberInfo memberInfo )
    {
      if ( memberInfo == null )
        return false;

      // Override hidden in inspector.
      if ( memberInfo.IsDefined( typeof( HideInInspector ), true ) )
        return false;

      // In general, don't show UnityEngine objects unless ShowInInspector is set.
      bool show = memberInfo.IsDefined( typeof( ShowInInspector ), true ) ||
                  !( memberInfo.DeclaringType.Namespace != null &&
                     memberInfo.DeclaringType.Namespace.Contains( "UnityEngine" ) );

      return show;
    }
  }
}
