using System.Collections;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes;
using DotnetOpenEhr.Rm.DataTypes.Basic;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Encapsulated;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.DataTypes.Uri;
using DotnetOpenEhr.Rm.Support;
using RmEvaluation = DotnetOpenEhr.Rm.Composition.Evaluation;
using RmAction = DotnetOpenEhr.Rm.Composition.Action;
using RmEvent = DotnetOpenEhr.Rm.DataStructures.Event;

namespace DotnetOpenEhr.Aql.Evaluation;

/// <summary>
/// RM-aware path-navigation helpers shared by <see cref="AqlEvaluator"/>
/// (row projection) and <see cref="DotnetOpenEhr.Aql.ArchetypePathResolver"/>
/// (single-root resolution). Lifted out of <c>AqlEvaluator</c> so the
/// two surfaces cannot drift on which RM attributes are walkable or
/// how predicates filter.
/// </summary>
/// <remarks>
/// Implementation is a closed switch over the supported RM types — no
/// reflection, no <c>Expression.Compile</c>, no <c>Reflection.Emit</c>
/// — so it remains trim- and Native-AOT-safe.
/// </remarks>
internal static class PathNavigator
{
    /// <summary>
    /// Walk a single attribute step. Returns either a scalar value or a
    /// <c>List&lt;object?&gt;</c> accumulator when the receiver was itself
    /// a collection. <c>null</c> means "no match" and short-circuits
    /// downstream steps.
    /// </summary>
    internal static object? GetAttribute(object? value, string name)
    {
        if (value is null) return null;
        if (value is string) return null;
        if (value is IEnumerable seq and not string and not IDictionary)
        {
            List<object?> results = [];
            foreach (object? item in seq)
            {
                object? sub = GetAttribute(item, name);
                if (sub is null) continue;
                if (sub is IEnumerable subSeq and not string)
                {
                    foreach (object? x in subSeq) results.Add(x);
                }
                else
                {
                    results.Add(sub);
                }
            }
            return results;
        }
        return GetSingleAttribute(value, name);
    }

    /// <summary>
    /// Filter a step's result by an optional node id and/or an optional
    /// name predicate. When both are <c>null</c>, the value is returned
    /// unchanged (safety net for callers that may pass through a "no
    /// predicate" step). Both predicates ANDed when both supplied.
    /// </summary>
    internal static object? FilterByPredicate(object? value, string? nodeId, string? name)
    {
        if (value is null) return null;
        if (nodeId is null && name is null) return value;

        if (value is IEnumerable seq and not string)
        {
            List<object?> filtered = [];
            foreach (object? item in seq)
            {
                if (Matches(item, nodeId, name))
                {
                    filtered.Add(item);
                }
            }
            return filtered;
        }
        return Matches(value, nodeId, name) ? value : null;
    }

    private static bool Matches(object? candidate, string? nodeId, string? name)
    {
        if (candidate is not Locatable loc) return false;
        if (nodeId is not null
            && !string.Equals(loc.ArchetypeNodeId, nodeId, StringComparison.Ordinal))
        {
            return false;
        }
        if (name is not null
            && !string.Equals(loc.Name?.Value, name, StringComparison.Ordinal))
        {
            return false;
        }
        return true;
    }

    private static object? GetSingleAttribute(object value, string name)
    {
        string n = name.ToLowerInvariant();
        // Two-step lookup: try the RM-specific switch first so the
        // canonical openEHR attribute names (snake_case) work, then
        // fall back to a couple of common Pascal-cased aliases for
        // ergonomics in expression strings.
        object? result = GetCanonicalAttribute(value, n);
        if (result is not null) return result;
        return GetCanonicalAttribute(value, name);
    }

    private static object? GetCanonicalAttribute(object value, string name)
    {
        if (value is Locatable locBase)
        {
            // Locatable base attributes are resolved here so every subtype
            // inherits them uniformly. Subtype arms below MUST NOT redefine
            // these six names with different semantics.
            object? baseHit = name switch
            {
                "name" => locBase.Name,
                "uid" => locBase.Uid,
                "archetype_node_id" => locBase.ArchetypeNodeId,
                "archetype_details" => locBase.ArchetypeDetails,
                "links" => locBase.Links,
                "feeder_audit" => locBase.FeederAudit,
                _ => null,
            };
            if (baseHit is not null) return baseHit;
        }
        // Subtype arms list attributes the subtype defines on top of
        // Locatable. The six Locatable base names (name, uid,
        // archetype_node_id, archetype_details, links, feeder_audit)
        // are resolved by the pre-check above and MUST NOT be repeated
        // here.
        return value switch
        {
        Composition c => name switch
        {
            "content" => c.Content,
            "context" => c.Context,
            "language" => c.Language,
            "territory" => c.Territory,
            "category" => c.Category,
            "composer" => c.Composer,
            _ => null,
        },
        EventContext ec => name switch
        {
            "start_time" => ec.StartTime,
            "end_time" => ec.EndTime,
            "location" => ec.Location,
            "setting" => ec.Setting,
            "other_context" => ec.OtherContext,
            "health_care_facility" => ec.HealthCareFacility,
            "participations" => ec.Participations,
            _ => null,
        },
        Section s => name switch
        {
            "items" => s.Items,
            _ => null,
        },
        Observation o => name switch
        {
            // Locatable base attrs (name, uid, archetype_node_id,
            // archetype_details, links, feeder_audit) inherited from the
            // GetCanonicalAttribute pre-check above.
            "data" => o.Data,
            "state" => o.State,
            "protocol" => o.Protocol,
            "subject" => o.Subject,
            "encoding" => o.Encoding,
            "language" => o.Language,
            "other_participations" => o.OtherParticipations,
            "workflow_id" => o.WorkflowId,
            "guideline_id" => o.GuidelineId,
            _ => null,
        },
        RmEvaluation ev => name switch
        {
            // Locatable base attrs (name, uid, archetype_node_id,
            // archetype_details, links, feeder_audit) inherited from the
            // GetCanonicalAttribute pre-check above.
            "data" => ev.Data,
            "protocol" => ev.Protocol,
            "subject" => ev.Subject,
            "encoding" => ev.Encoding,
            "language" => ev.Language,
            _ => null,
        },
        Instruction ins => name switch
        {
            // Locatable base attrs (name, uid, archetype_node_id,
            // archetype_details, links, feeder_audit) inherited from the
            // GetCanonicalAttribute pre-check above.
            "activities" => ins.Activities,
            "narrative" => ins.Narrative,
            "expiry_time" => ins.ExpiryTime,
            "protocol" => ins.Protocol,
            "subject" => ins.Subject,
            "encoding" => ins.Encoding,
            "language" => ins.Language,
            _ => null,
        },
        RmAction act => name switch
        {
            // Locatable base attrs (name, uid, archetype_node_id,
            // archetype_details, links, feeder_audit) inherited from the
            // GetCanonicalAttribute pre-check above.
            "time" => act.Time,
            "description" => act.Description,
            "ism_transition" => act.IsmTransition,
            "instruction_details" => act.InstructionDetails,
            "protocol" => act.Protocol,
            "subject" => act.Subject,
            "encoding" => act.Encoding,
            "language" => act.Language,
            _ => null,
        },
        AdminEntry ae => name switch
        {
            // Locatable base attrs (name, uid, archetype_node_id,
            // archetype_details, links, feeder_audit) inherited from the
            // GetCanonicalAttribute pre-check above.
            "data" => ae.Data,
            "subject" => ae.Subject,
            "encoding" => ae.Encoding,
            "language" => ae.Language,
            _ => null,
        },
        Activity a => name switch
        {
            // Locatable base attrs (name, uid, archetype_node_id,
            // archetype_details, links, feeder_audit) inherited from the
            // GetCanonicalAttribute pre-check above.
            "description" => a.Description,
            "timing" => a.Timing,
            "action_archetype_id" => a.ActionArchetypeId,
            _ => null,
        },
        History h => name switch
        {
            // Locatable base attrs (name, uid, archetype_node_id,
            // archetype_details, links, feeder_audit) inherited from the
            // GetCanonicalAttribute pre-check above.
            "origin" => h.Origin,
            "events" => h.Events,
            "period" => h.Period,
            "duration" => h.Duration,
            "summary" => h.Summary,
            _ => null,
        },
        IntervalEvent iev => name switch
        {
            // Locatable base attrs (name, uid, archetype_node_id,
            // archetype_details, links, feeder_audit) inherited from the
            // GetCanonicalAttribute pre-check above.
            "time" => iev.Time,
            "data" => iev.Data,
            "state" => iev.State,
            "width" => iev.Width,
            "sample_count" => iev.SampleCount,
            "math_function" => iev.MathFunction,
            _ => null,
        },
        RmEvent e => name switch
        {
            // Locatable base attrs (name, uid, archetype_node_id,
            // archetype_details, links, feeder_audit) inherited from the
            // GetCanonicalAttribute pre-check above.
            "time" => e.Time,
            "data" => e.Data,
            "state" => e.State,
            _ => null,
        },
        ItemTree it => name switch
        {
            // Locatable base attrs (name, uid, archetype_node_id,
            // archetype_details, links, feeder_audit) inherited from the
            // GetCanonicalAttribute pre-check above.
            "items" => it.Items,
            _ => null,
        },
        ItemList il => name switch
        {
            // Locatable base attrs (name, uid, archetype_node_id,
            // archetype_details, links, feeder_audit) inherited from the
            // GetCanonicalAttribute pre-check above.
            "items" => il.Items,
            _ => null,
        },
        ItemSingle iss => name switch
        {
            // Locatable base attrs (name, uid, archetype_node_id,
            // archetype_details, links, feeder_audit) inherited from the
            // GetCanonicalAttribute pre-check above.
            "item" => iss.Item,
            _ => null,
        },
        ItemTable itb => name switch
        {
            // Locatable base attrs (name, uid, archetype_node_id,
            // archetype_details, links, feeder_audit) inherited from the
            // GetCanonicalAttribute pre-check above.
            "rows" => itb.Rows,
            _ => null,
        },
        Cluster cl => name switch
        {
            // Locatable base attrs (name, uid, archetype_node_id,
            // archetype_details, links, feeder_audit) inherited from the
            // GetCanonicalAttribute pre-check above.
            "items" => cl.Items,
            _ => null,
        },
        Element el => name switch
        {
            // Locatable base attrs (name, uid, archetype_node_id,
            // archetype_details, links, feeder_audit) inherited from the
            // GetCanonicalAttribute pre-check above.
            "value" => el.Value,
            "null_flavour" => el.NullFlavour,
            "null_reason" => el.NullReason,
            _ => null,
        },
        DvCodedText dct => name switch
        {
            "value" => dct.Value,
            "defining_code" => dct.DefiningCode,
            "mappings" => dct.Mappings,
            "language" => dct.Language,
            "encoding" => dct.Encoding,
            "formatting" => dct.Formatting,
            "hyperlink" => dct.Hyperlink,
            _ => null,
        },
        DvText dt => name switch
        {
            "value" => dt.Value,
            "mappings" => dt.Mappings,
            "language" => dt.Language,
            "encoding" => dt.Encoding,
            "formatting" => dt.Formatting,
            "hyperlink" => dt.Hyperlink,
            _ => null,
        },
        DvQuantity dq => name switch
        {
            "magnitude" => dq.Magnitude,
            "units" => dq.Units,
            "precision" => dq.Precision,
            "units_system" => dq.UnitsSystem,
            "units_display_name" => dq.UnitsDisplayName,
            "normal_range" => dq.NormalRange,
            "normal_status" => dq.NormalStatus,
            "accuracy" => dq.Accuracy,
            "accuracy_is_percent" => dq.AccuracyIsPercent,
            "magnitude_status" => dq.MagnitudeStatus,
            _ => null,
        },
        DvCount dc => name switch
        {
            "magnitude" => (object?)dc.Magnitude,
            _ => null,
        },
        DvProportion dp => name switch
        {
            "numerator" => dp.Numerator,
            "denominator" => dp.Denominator,
            "type" => dp.Type,
            "precision" => dp.Precision,
            _ => null,
        },
        DvOrdinal dor => name switch
        {
            "value" => dor.Value,
            "symbol" => dor.Symbol,
            _ => null,
        },
        DvScale dsc => name switch
        {
            "value" => dsc.Value,
            "symbol" => dsc.Symbol,
            _ => null,
        },
        DvDate dd => name switch { "value" => dd.Value, _ => null },
        DvTime dtm => name switch { "value" => dtm.Value, _ => null },
        DvDateTime ddt => name switch { "value" => ddt.Value, _ => null },
        DvDuration ddu => name switch { "value" => ddu.Value, _ => null },
        DvBoolean db => name switch { "value" => (object?)db.Value, _ => null },
        DvUri du => name switch { "value" => du.Value, _ => null },
        DvIdentifier di => name switch
        {
            "id" => di.Id,
            "type" => di.Type,
            "issuer" => di.Issuer,
            "assigner" => di.Assigner,
            _ => null,
        },
        CodePhrase cp => name switch
        {
            "code_string" => cp.CodeString,
            "terminology_id" => cp.TerminologyId,
            "preferred_term" => cp.PreferredTerm,
            _ => null,
        },
        UidBasedId u => name switch { "value" => u.Value, _ => null },
        ObjectId oid => name switch { "value" => oid.Value, _ => null },
        Archetyped ar => name switch
        {
            "archetype_id" => ar.ArchetypeId,
            "template_id" => ar.TemplateId,
            "rm_version" => ar.RmVersion,
            _ => null,
        },
        PartyIdentified pi => name switch
        {
            "name" => pi.Name,
            "identifiers" => pi.Identifiers,
            "external_ref" => pi.ExternalRef,
            _ => null,
        },
        _ => null,
        };
    }
}
