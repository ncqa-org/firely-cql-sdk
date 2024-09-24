using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;

namespace Hl7.Cql.Elm.Visitor
{
    public class DataRequirementsFilter: SerializationFilter
    {
        private DataRequirement[] _requirements;
        private Stack<string?> _propertyStack = new();
        private DataRequirement? _currentFilter = null;
        
        // Code list tracking
        private List<bool> _keepCodeMap = new();
        private int _codeIndex = 0;

        // Flag to skip all elements in a resource
        private bool _discardResource = false;

        public DataRequirementsFilter(IEnumerable<DataRequirement> requirements)
        {
            _requirements = requirements.ToArray();
        }

        public override void EnterObject(object value, ClassMapping? mapping)
        {
        }

        public override bool TryEnterMember(string name, object value, PropertyMapping? mapping)
        {
            // Filter top level resources
            if (mapping.ImplementingType == typeof(Bundle.EntryComponent))
            {
                _propertyStack.Push(null);
                return true;
            }

            if (value is Resource resource)
            {
                if (_currentFilter != null)
                {
                    throw new InvalidOperationException("Resource encountered in invalid state.");
                }
                
                var dr = _requirements.SingleOrDefault(o => o.Type.ToString() == resource.TypeName);
                
                // Discard entire resources
                if (dr == null)
                {
                    _discardResource = true;
                }

                _currentFilter = dr;
                _propertyStack.Push(resource.TypeName);
                return true;
            }

            // Not inside a resource yet
            if (_currentFilter == null)
            {
                _propertyStack.Push(null);
                return true;
            }

            if (_discardResource)
            {
                return false;
            }

            // Path of parent property, not including this property
            var s = _propertyStack
                .Where(o => o is not null)
                .Reverse().ToArray();

            var propertyPath = string.Join(".", s);

            // Path of the property relative to the resource we are in
            var d = string.Join(".", s.Skip(1)) + name;
            
            // Filter properties only from the top level resource

            if (s.Length == 1 && _currentFilter.MustSupport.All(o => d != o))
            {
                return false;
            }

            var codeFilters =
                _currentFilter.CodeFilter.Where(o => o.Path == propertyPath);

            // We are entering a list of codes, identify which indices to keep
            // based on a code filter (if any)
            if (mapping.ImplementingType == typeof(Coding))
            {
                var codeFilter = codeFilters.SingleOrDefault(o => o.Code.Count > 0);

                if (codeFilter != null)
                {
                    var codes = (IEnumerable<Coding>)value;
                    foreach (var code in codes)
                    {
                        var keep = codeFilter.Code.Any(o => o.Code == code.Code && o.System == code.System);

                        // Add twice for both code/system
                        _keepCodeMap.Add(keep);
                        _keepCodeMap.Add(keep);
                    }

                    // If we are not keeping any codes, discard the entire list
                    if (_keepCodeMap.Count == 0 || _keepCodeMap.All(o => !o))
                    {
                        _keepCodeMap.Clear();
                        return false;
                    }
                }

                // Discard all codes with no filter
                else
                {
                    return false;
                }
            }

            // Encountering a code or system
            if (_keepCodeMap.Count != 0 && value is Hl7.Fhir.Model.Code or FhirUri)
            {
                // If within an extension, never filter
                if (_propertyStack.All(o => o is not "extension"))
                {
                    var keepIndex = _codeIndex++;
                    if (!_keepCodeMap[keepIndex])
                    {
                        return false;
                    }
                }
            }

            // Record property name
            _propertyStack.Push(name);
            return true;
        }

        public override void LeaveMember(string name, object value, PropertyMapping? mapping)
        {
            if (value is Resource)
            {
                if (!_discardResource && _currentFilter == null) throw new InvalidOperationException();

                _currentFilter = null;
                _discardResource = false;
            }

            if (mapping.ImplementingType == typeof(Coding))
            {
                if (_keepCodeMap.Count != _codeIndex)
                    throw new InvalidOperationException("Did not visit the expected number of codes");

                _keepCodeMap.Clear();
                _codeIndex = 0;
            }

            _propertyStack.Pop();
        }

        public override void LeaveObject(object value, ClassMapping? mapping)
        {

        }
    }
}
