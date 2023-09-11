using System;
using System.Linq;
using System.Collections.Generic;
using Hl7.Cql.Runtime;
using Hl7.Cql.Primitives;
using Hl7.Cql.Abstractions;
using Hl7.Cql.ValueSets;
using Hl7.Cql.Iso8601;
using Hl7.Fhir.Model;
using Range = Hl7.Fhir.Model.Range;
using Task = Hl7.Fhir.Model.Task;

[System.CodeDom.Compiler.GeneratedCode(".NET Code Generation", "0.9.0.0")]
[CqlLibrary("PrimaryCariesPreventionasOfferedbyPCPsincludingDentistsFHIR", "0.0.008")]
public class PrimaryCariesPreventionasOfferedbyPCPsincludingDentistsFHIR_0_0_008
{


    internal CqlContext context;

    #region Cached values

    internal Lazy<CqlValueSet> __Clinical_Oral_Evaluation;
    internal Lazy<CqlValueSet> __Fluoride_Varnish_Application_for_Children;
    internal Lazy<CqlValueSet> __Office_Visit;
    internal Lazy<CqlValueSet> __Online_Assessments;
    internal Lazy<CqlValueSet> __Preventive_Care___Established_Office_Visit__0_to_17;
    internal Lazy<CqlValueSet> __Preventive_Care_Services___Established_Office_Visit__18_and_Up;
    internal Lazy<CqlValueSet> __Preventive_Care_Services_Initial_Office_Visit__18_and_Up;
    internal Lazy<CqlValueSet> __Preventive_Care__Initial_Office_Visit__0_to_17;
    internal Lazy<CqlValueSet> __Telephone_Visits;
    internal Lazy<CqlCode> __Birth_date;
    internal Lazy<CqlCode[]> __LOINC;
    internal Lazy<CqlInterval<CqlDateTime>> __Measurement_Period;
    internal Lazy<Patient> __Patient;
    internal Lazy<IEnumerable<Coding>> __SDE_Ethnicity;
    internal Lazy<IEnumerable<Tuples.Tuple_CFQHSgYJOXjAOCKdWLdZNNHDG>> __SDE_Payer;
    internal Lazy<IEnumerable<Coding>> __SDE_Race;
    internal Lazy<CqlCode> __SDE_Sex;
    internal Lazy<IEnumerable<Encounter>> __Qualifying_Encounters;
    internal Lazy<bool?> __Initial_Population;
    internal Lazy<bool?> __Denominator;
    internal Lazy<bool?> __Denominator_Exclusions;
    internal Lazy<bool?> __Stratification_1;
    internal Lazy<bool?> __Stratification_2;
    internal Lazy<bool?> __Stratification_3;
    internal Lazy<bool?> __Numerator;

    #endregion
    public PrimaryCariesPreventionasOfferedbyPCPsincludingDentistsFHIR_0_0_008(CqlContext context)
    {
        this.context = context ?? throw new ArgumentNullException("context");

        FHIRHelpers_4_0_001 = new FHIRHelpers_4_0_001(context);
        SupplementalDataElementsFHIR4_2_0_000 = new SupplementalDataElementsFHIR4_2_0_000(context);
        MATGlobalCommonFunctionsFHIR4_6_1_000 = new MATGlobalCommonFunctionsFHIR4_6_1_000(context);
        HospiceFHIR4_2_3_000 = new HospiceFHIR4_2_3_000(context);

        __Clinical_Oral_Evaluation = new Lazy<CqlValueSet>(this.Clinical_Oral_Evaluation_Value);
        __Fluoride_Varnish_Application_for_Children = new Lazy<CqlValueSet>(this.Fluoride_Varnish_Application_for_Children_Value);
        __Office_Visit = new Lazy<CqlValueSet>(this.Office_Visit_Value);
        __Online_Assessments = new Lazy<CqlValueSet>(this.Online_Assessments_Value);
        __Preventive_Care___Established_Office_Visit__0_to_17 = new Lazy<CqlValueSet>(this.Preventive_Care___Established_Office_Visit__0_to_17_Value);
        __Preventive_Care_Services___Established_Office_Visit__18_and_Up = new Lazy<CqlValueSet>(this.Preventive_Care_Services___Established_Office_Visit__18_and_Up_Value);
        __Preventive_Care_Services_Initial_Office_Visit__18_and_Up = new Lazy<CqlValueSet>(this.Preventive_Care_Services_Initial_Office_Visit__18_and_Up_Value);
        __Preventive_Care__Initial_Office_Visit__0_to_17 = new Lazy<CqlValueSet>(this.Preventive_Care__Initial_Office_Visit__0_to_17_Value);
        __Telephone_Visits = new Lazy<CqlValueSet>(this.Telephone_Visits_Value);
        __Birth_date = new Lazy<CqlCode>(this.Birth_date_Value);
        __LOINC = new Lazy<CqlCode[]>(this.LOINC_Value);
        __Measurement_Period = new Lazy<CqlInterval<CqlDateTime>>(this.Measurement_Period_Value);
        __Patient = new Lazy<Patient>(this.Patient_Value);
        __SDE_Ethnicity = new Lazy<IEnumerable<Coding>>(this.SDE_Ethnicity_Value);
        __SDE_Payer = new Lazy<IEnumerable<Tuples.Tuple_CFQHSgYJOXjAOCKdWLdZNNHDG>>(this.SDE_Payer_Value);
        __SDE_Race = new Lazy<IEnumerable<Coding>>(this.SDE_Race_Value);
        __SDE_Sex = new Lazy<CqlCode>(this.SDE_Sex_Value);
        __Qualifying_Encounters = new Lazy<IEnumerable<Encounter>>(this.Qualifying_Encounters_Value);
        __Initial_Population = new Lazy<bool?>(this.Initial_Population_Value);
        __Denominator = new Lazy<bool?>(this.Denominator_Value);
        __Denominator_Exclusions = new Lazy<bool?>(this.Denominator_Exclusions_Value);
        __Stratification_1 = new Lazy<bool?>(this.Stratification_1_Value);
        __Stratification_2 = new Lazy<bool?>(this.Stratification_2_Value);
        __Stratification_3 = new Lazy<bool?>(this.Stratification_3_Value);
        __Numerator = new Lazy<bool?>(this.Numerator_Value);
    }
    #region Dependencies

    public FHIRHelpers_4_0_001 FHIRHelpers_4_0_001 { get; }
    public SupplementalDataElementsFHIR4_2_0_000 SupplementalDataElementsFHIR4_2_0_000 { get; }
    public MATGlobalCommonFunctionsFHIR4_6_1_000 MATGlobalCommonFunctionsFHIR4_6_1_000 { get; }
    public HospiceFHIR4_2_3_000 HospiceFHIR4_2_3_000 { get; }

    #endregion

    private CqlValueSet Clinical_Oral_Evaluation_Value()
    {
        return new CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.464.1003.125.12.1003", 
			null);
    }

    [CqlDeclaration("Clinical Oral Evaluation")]
    [CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.464.1003.125.12.1003")]
    public CqlValueSet Clinical_Oral_Evaluation() => __Clinical_Oral_Evaluation.Value;

    private CqlValueSet Fluoride_Varnish_Application_for_Children_Value()
    {
        return new CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.464.1003.125.12.1002", 
			null);
    }

    [CqlDeclaration("Fluoride Varnish Application for Children")]
    [CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.464.1003.125.12.1002")]
    public CqlValueSet Fluoride_Varnish_Application_for_Children() => __Fluoride_Varnish_Application_for_Children.Value;

    private CqlValueSet Office_Visit_Value()
    {
        return new CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.464.1003.101.12.1001", 
			null);
    }

    [CqlDeclaration("Office Visit")]
    [CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.464.1003.101.12.1001")]
    public CqlValueSet Office_Visit() => __Office_Visit.Value;

    private CqlValueSet Online_Assessments_Value()
    {
        return new CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.464.1003.101.12.1089", 
			null);
    }

    [CqlDeclaration("Online Assessments")]
    [CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.464.1003.101.12.1089")]
    public CqlValueSet Online_Assessments() => __Online_Assessments.Value;

    private CqlValueSet Preventive_Care___Established_Office_Visit__0_to_17_Value()
    {
        return new CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.464.1003.101.12.1024", 
			null);
    }

    [CqlDeclaration("Preventive Care - Established Office Visit, 0 to 17")]
    [CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.464.1003.101.12.1024")]
    public CqlValueSet Preventive_Care___Established_Office_Visit__0_to_17() => __Preventive_Care___Established_Office_Visit__0_to_17.Value;

    private CqlValueSet Preventive_Care_Services___Established_Office_Visit__18_and_Up_Value()
    {
        return new CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.464.1003.101.12.1025", 
			null);
    }

    [CqlDeclaration("Preventive Care Services - Established Office Visit, 18 and Up")]
    [CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.464.1003.101.12.1025")]
    public CqlValueSet Preventive_Care_Services___Established_Office_Visit__18_and_Up() => __Preventive_Care_Services___Established_Office_Visit__18_and_Up.Value;

    private CqlValueSet Preventive_Care_Services_Initial_Office_Visit__18_and_Up_Value()
    {
        return new CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.464.1003.101.12.1023", 
			null);
    }

    [CqlDeclaration("Preventive Care Services-Initial Office Visit, 18 and Up")]
    [CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.464.1003.101.12.1023")]
    public CqlValueSet Preventive_Care_Services_Initial_Office_Visit__18_and_Up() => __Preventive_Care_Services_Initial_Office_Visit__18_and_Up.Value;

    private CqlValueSet Preventive_Care__Initial_Office_Visit__0_to_17_Value()
    {
        return new CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.464.1003.101.12.1022", 
			null);
    }

    [CqlDeclaration("Preventive Care- Initial Office Visit, 0 to 17")]
    [CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.464.1003.101.12.1022")]
    public CqlValueSet Preventive_Care__Initial_Office_Visit__0_to_17() => __Preventive_Care__Initial_Office_Visit__0_to_17.Value;

    private CqlValueSet Telephone_Visits_Value()
    {
        return new CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.464.1003.101.12.1080", 
			null);
    }

    [CqlDeclaration("Telephone Visits")]
    [CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.464.1003.101.12.1080")]
    public CqlValueSet Telephone_Visits() => __Telephone_Visits.Value;

    private CqlCode Birth_date_Value()
    {
        return new CqlCode("21112-8", 
			"http://loinc.org", 
			null, 
			null);
    }

    [CqlDeclaration("Birth date")]
    public CqlCode Birth_date() => __Birth_date.Value;

    private CqlCode[] LOINC_Value()
    {
        var a_ = new CqlCode("21112-8", 
			"http://loinc.org", 
			null, 
			null);
        return new CqlCode[]
		{
			a_,
		};
    }
    [CqlDeclaration("LOINC")]
    public CqlCode[] LOINC() => __LOINC.Value;

    private CqlInterval<CqlDateTime> Measurement_Period_Value()
    {
        return ((CqlInterval<CqlDateTime>)context.ResolveParameter("PrimaryCariesPreventionasOfferedbyPCPsincludingDentistsFHIR-0.0.008", 
			"Measurement Period", 
			null));
    }

    [CqlDeclaration("Measurement Period")]
    public CqlInterval<CqlDateTime> Measurement_Period() => __Measurement_Period.Value;

    private Patient Patient_Value()
    {
        var a_ = context?.Operators.RetrieveByValueSet<Patient>(null, 
			null);
        return context?.Operators.SingleOrNull<Patient>(a_);
    }
    [CqlDeclaration("Patient")]
    public Patient Patient() => __Patient.Value;

    private IEnumerable<Coding> SDE_Ethnicity_Value()
    {
        return SupplementalDataElementsFHIR4_2_0_000.SDE_Ethnicity();
    }
    [CqlDeclaration("SDE Ethnicity")]
    public IEnumerable<Coding> SDE_Ethnicity() => __SDE_Ethnicity.Value;

    private IEnumerable<Tuples.Tuple_CFQHSgYJOXjAOCKdWLdZNNHDG> SDE_Payer_Value()
    {
        return SupplementalDataElementsFHIR4_2_0_000.SDE_Payer();
    }
    [CqlDeclaration("SDE Payer")]
    public IEnumerable<Tuples.Tuple_CFQHSgYJOXjAOCKdWLdZNNHDG> SDE_Payer() => __SDE_Payer.Value;

    private IEnumerable<Coding> SDE_Race_Value()
    {
        return SupplementalDataElementsFHIR4_2_0_000.SDE_Race();
    }
    [CqlDeclaration("SDE Race")]
    public IEnumerable<Coding> SDE_Race() => __SDE_Race.Value;

    private CqlCode SDE_Sex_Value()
    {
        return SupplementalDataElementsFHIR4_2_0_000.SDE_Sex();
    }
    [CqlDeclaration("SDE Sex")]
    public CqlCode SDE_Sex() => __SDE_Sex.Value;

    private IEnumerable<Encounter> Qualifying_Encounters_Value()
    {
        var a_ = this.Office_Visit();
        var b_ = context?.Operators.RetrieveByValueSet<Encounter>(a_, 
			typeof(Encounter).GetProperty("Type"));
        var c_ = this.Preventive_Care___Established_Office_Visit__0_to_17();
        var d_ = context?.Operators.RetrieveByValueSet<Encounter>(c_, 
			typeof(Encounter).GetProperty("Type"));
        var e_ = context?.Operators.ListUnion<Encounter>(b_, 
			d_);
        var f_ = this.Preventive_Care__Initial_Office_Visit__0_to_17();
        var g_ = context?.Operators.RetrieveByValueSet<Encounter>(f_, 
			typeof(Encounter).GetProperty("Type"));
        var h_ = this.Preventive_Care_Services___Established_Office_Visit__18_and_Up();
        var i_ = context?.Operators.RetrieveByValueSet<Encounter>(h_, 
			typeof(Encounter).GetProperty("Type"));
        var j_ = context?.Operators.ListUnion<Encounter>(g_, 
			i_);
        var k_ = context?.Operators.ListUnion<Encounter>(e_, 
			j_);
        var l_ = this.Preventive_Care_Services_Initial_Office_Visit__18_and_Up();
        var m_ = context?.Operators.RetrieveByValueSet<Encounter>(l_, 
			typeof(Encounter).GetProperty("Type"));
        var n_ = this.Clinical_Oral_Evaluation();
        var o_ = context?.Operators.RetrieveByValueSet<Encounter>(n_, 
			typeof(Encounter).GetProperty("Type"));
        var p_ = context?.Operators.ListUnion<Encounter>(m_, 
			o_);
        var q_ = context?.Operators.ListUnion<Encounter>(k_, 
			p_);
        var r_ = this.Telephone_Visits();
        var s_ = context?.Operators.RetrieveByValueSet<Encounter>(r_, 
			typeof(Encounter).GetProperty("Type"));
        var t_ = this.Online_Assessments();
        var u_ = context?.Operators.RetrieveByValueSet<Encounter>(t_, 
			typeof(Encounter).GetProperty("Type"));
        var v_ = context?.Operators.ListUnion<Encounter>(s_, 
			u_);
        var w_ = context?.Operators.ListUnion<Encounter>(q_, 
			v_);
        Func<Encounter,bool?> af_ = (ValidEncounter) => 
        {
            var x_ = this.Measurement_Period();
            var y_ = (ValidEncounter?.Period as object);
            var z_ = MATGlobalCommonFunctionsFHIR4_6_1_000.Normalize_Interval(y_);
            var aa_ = context?.Operators.IntervalIncludesInterval<CqlDateTime>(x_, 
				z_, 
				null);
            var ac_ = (ValidEncounter?.StatusElement as object);
            var ab_ = (context?.Operators.Convert<string>(ac_) as object);
            var ad_ = ("finished" as object);
            var ae_ = context?.Operators.Equal(ab_, 
				ad_);
            return context?.Operators.And(aa_, 
				ae_);
        };
        return context?.Operators.WhereOrNull<Encounter>(w_, 
			af_);
    }
    [CqlDeclaration("Qualifying Encounters")]
    public IEnumerable<Encounter> Qualifying_Encounters() => __Qualifying_Encounters.Value;

    private bool? Initial_Population_Value()
    {
        var b_ = (this.Patient()?.BirthDateElement?.Value as object);
        var c_ = context?.Operators.Convert<CqlDate>(b_);
        var d_ = this.Measurement_Period();
        var e_ = context?.Operators.Start(d_);
        var f_ = context?.Operators.DateFrom(e_);
        var a_ = (context?.Operators.CalculateAgeAt(c_, 
			f_, 
			"month") as object);
        var g_ = (((int?)6) as object);
        var h_ = context?.Operators.GreaterOrEqual(a_, 
			g_);
        var i_ = (context?.Operators.CalculateAgeAt(c_, 
			f_, 
			"year") as object);
        var o_ = (((int?)20) as object);
        var p_ = context?.Operators.Less(i_, 
			o_);
        var q_ = context?.Operators.And(h_, 
			p_);
        var r_ = this.Qualifying_Encounters();
        var s_ = context?.Operators.ExistsInList<Encounter>(r_);
        return context?.Operators.And(q_, 
			s_);
    }
    [CqlDeclaration("Initial Population")]
    public bool? Initial_Population() => __Initial_Population.Value;

    private bool? Denominator_Value()
    {
        return this.Initial_Population();
    }
    [CqlDeclaration("Denominator")]
    public bool? Denominator() => __Denominator.Value;

    private bool? Denominator_Exclusions_Value()
    {
        return HospiceFHIR4_2_3_000.Has_Hospice();
    }
    [CqlDeclaration("Denominator Exclusions")]
    public bool? Denominator_Exclusions() => __Denominator_Exclusions.Value;

    private bool? Stratification_1_Value()
    {
        var b_ = (this.Patient()?.BirthDateElement?.Value as object);
        var c_ = context?.Operators.Convert<CqlDate>(b_);
        var d_ = this.Measurement_Period();
        var e_ = context?.Operators.Start(d_);
        var f_ = context?.Operators.DateFrom(e_);
        var a_ = (context?.Operators.CalculateAgeAt(c_, 
			f_, 
			"month") as object);
        var g_ = (((int?)6) as object);
        var h_ = context?.Operators.GreaterOrEqual(a_, 
			g_);
        var i_ = (context?.Operators.CalculateAgeAt(c_, 
			f_, 
			"year") as object);
        var o_ = (((int?)4) as object);
        var p_ = context?.Operators.LessOrEqual(i_, 
			o_);
        return context?.Operators.And(h_, 
			p_);
    }
    [CqlDeclaration("Stratification 1")]
    public bool? Stratification_1() => __Stratification_1.Value;

    private bool? Stratification_2_Value()
    {
        var a_ = (this.Patient()?.BirthDateElement?.Value as object);
        var b_ = context?.Operators.Convert<CqlDate>(a_);
        var c_ = this.Measurement_Period();
        var d_ = context?.Operators.Start(c_);
        var e_ = context?.Operators.DateFrom(d_);
        var f_ = context?.Operators.CalculateAgeAt(b_, 
			e_, 
			"year");
        var g_ = context?.Operators.Interval(((int?)5), 
			((int?)11), 
			true, 
			true);
        return context?.Operators.ElementInInterval<int?>(f_, 
			g_, 
			null);
    }
    [CqlDeclaration("Stratification 2")]
    public bool? Stratification_2() => __Stratification_2.Value;

    private bool? Stratification_3_Value()
    {
        var a_ = (this.Patient()?.BirthDateElement?.Value as object);
        var b_ = context?.Operators.Convert<CqlDate>(a_);
        var c_ = this.Measurement_Period();
        var d_ = context?.Operators.Start(c_);
        var e_ = context?.Operators.DateFrom(d_);
        var f_ = context?.Operators.CalculateAgeAt(b_, 
			e_, 
			"year");
        var g_ = context?.Operators.Interval(((int?)12), 
			((int?)20), 
			true, 
			false);
        return context?.Operators.ElementInInterval<int?>(f_, 
			g_, 
			null);
    }
    [CqlDeclaration("Stratification 3")]
    public bool? Stratification_3() => __Stratification_3.Value;

    private bool? Numerator_Value()
    {
        var a_ = this.Fluoride_Varnish_Application_for_Children();
        var b_ = context?.Operators.RetrieveByValueSet<Procedure>(a_, 
			typeof(Procedure).GetProperty("Code"));
        Func<Procedure,bool?> k_ = (FluorideApplication) => 
        {
            var c_ = this.Measurement_Period();
            var d_ = (FluorideApplication?.Performed as object);
            var e_ = MATGlobalCommonFunctionsFHIR4_6_1_000.Normalize_Interval(d_);
            var f_ = context?.Operators.IntervalIncludesInterval<CqlDateTime>(c_, 
				e_, 
				null);
            var h_ = (FluorideApplication?.StatusElement as object);
            var g_ = (context?.Operators.Convert<string>(h_) as object);
            var i_ = ("completed" as object);
            var j_ = context?.Operators.Equal(g_, 
				i_);
            return context?.Operators.And(f_, 
				j_);
        };
        var l_ = context?.Operators.WhereOrNull<Procedure>(b_, 
			k_);
        return context?.Operators.ExistsInList<Procedure>(l_);
    }
    [CqlDeclaration("Numerator")]
    public bool? Numerator() => __Numerator.Value;

}