using reromanlee.ReactiveLocalizer.Unity;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace reromanlee.ReactiveLocalizer.Samples.UIToolkit
{
    /// <summary>
    /// Shows entries in the text elements of its GameObject's <see cref="UIDocument"/>, found by name, with no code and
    /// on every Unity version: each pair names an element of the document's UXML and the entry it shows.
    /// </summary>
    /// <remarks>
    /// The document rebuilds its elements whenever it's enabled or its UXML changes; the new elements are found and
    /// bound again on their first layout. In Edit Mode it previews the entries in the editor's preview language, which
    /// never reaches a file, since UXML keeps its own text.
    /// </remarks>
    [ExecuteAlways]
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("ReactiveLocalizer/Localized Document")]
    public sealed class LocalizedDocument : MonoBehaviour
    {
        [Tooltip("Each text element, by name, and the entry it shows.")]
        [SerializeField] private List<ElementEntry> _elements = new();

        private readonly List<ElementTextBinding> _bindings = new();
        private UIDocument _document;
        private VisualElement _root;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            Watch();
            BindElements();
        }

        private void OnDisable()
        {
            if (_root != null)
            {
                _root.UnregisterCallback<GeometryChangedEvent>(OnLayout);
                _root = null;
            }
            Unbind();
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled)
            {
                Unbind();
                BindElements();
            }
        }

        /// <summary>Finds the document's root, which is new after every rebuild, and binds its elements on their first layout.</summary>
        private void Watch()
        {
            VisualElement root = _document != null ? _document.rootVisualElement : null;
            if (root == _root)
            {
                return;
            }
            _root?.UnregisterCallback<GeometryChangedEvent>(OnLayout);
            _root = root;
            _root?.RegisterCallback<GeometryChangedEvent>(OnLayout);
        }

        private void OnLayout(GeometryChangedEvent change)
        {
            Watch();
            BindElements();
        }

        private void BindElements()
        {
            if (_root == null)
            {
                return;
            }
            for (int i = 0; i < _elements.Count; i++)
            {
                TextElement element = string.IsNullOrEmpty(_elements[i].Element) ? null : _root.Q<TextElement>(_elements[i].Element);
                bool isBound = i < _bindings.Count && _bindings[i] != null && _bindings[i].Element == element;
                if (isBound || element == null || _elements[i].Entry.IsEmpty)
                {
                    continue;
                }
                while (_bindings.Count <= i)
                {
                    _bindings.Add(null);
                }
                _bindings[i]?.Dispose();
                _bindings[i] = element.BindText(_elements[i].Entry);
            }
        }

        private void Unbind()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                _bindings[i]?.Dispose();
            }
            _bindings.Clear();
        }

        /// <summary>A text element of the document, by name, and the entry it shows.</summary>
        [Serializable]
        private struct ElementEntry
        {
            [Tooltip("The element's name in the UXML.")]
            [SerializeField] private string _element;

            [SerializeField] private EntryReference _entry;

            public string Element => _element;

            public EntryReference Entry => _entry;
        }
    }
}
