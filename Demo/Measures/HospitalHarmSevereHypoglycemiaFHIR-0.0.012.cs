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
[System.CodeDom.Compiler.GeneratedCode(".NET Code Generation", "1.0.0.0")]
[CqlLibrary("HospitalHarmSevereHypoglycemiaFHIR", "0.0.012")]
public class HospitalHarmSevereHypoglycemiaFHIR_0_0_012
{

    internal CqlContext context;

    #region Cached values

    internal Lazy<CqlValueSet> __birth_date;
    internal Lazy<CqlValueSet> __Emergency_Department_Visit;
    internal Lazy<CqlValueSet> __Encounter_Inpatient;
    internal Lazy<CqlValueSet> __Glucose_lab_test;
    internal Lazy<CqlValueSet> __Hypoglycemics;
    internal Lazy<CqlValueSet> __Hypoglycemics_Severe_Hypoglycemia;
    internal Lazy<CqlValueSet> __Observation_Services;
    internal Lazy<CqlCode> __Birth_date;
    internal Lazy<CqlCode[]> __LOINC;
    internal Lazy<CqlInterval<CqlDateTime>> __Measurement_Period;
    internal Lazy<Patient> __Patient;
    internal Lazy<IEnumerable<Tuples.Tuple_CFQHSgYJOXjAOCKdWLdZNNHDG>> __SDE_Payer;
    internal Lazy<IEnumerable<Coding>> __SDE_Race;
    internal Lazy<CqlCode> __SDE_Sex;
    internal Lazy<IEnumerable<Encounter>> __Inpatient_Encounter_During_Measurement_Period;
    internal Lazy<IEnumerable<Encounter>> __Qualifying_Encounter;
    internal Lazy<IEnumerable<MedicationAdministration>> __Hypoglycemic_Medication_Administration;
    internal Lazy<IEnumerable<Encounter>> __Qualifying_Encounter_with_Hypoglycemic_Medication_Administration;
    internal Lazy<IEnumerable<Encounter>> __Initial_Population;
    internal Lazy<IEnumerable<Encounter>> __Denominator;
    internal Lazy<IEnumerable<Encounter>> __Severe_Hypoglycemic_Harm_Event;
    internal Lazy<IEnumerable<Encounter>> __Numerator;
    internal Lazy<IEnumerable<Coding>> __SDE_Ethnicity;

    #endregion
    public HospitalHarmSevereHypoglycemiaFHIR_0_0_012(CqlContext context)
    {
        this.context = context ?? throw new ArgumentNullException("context");

        FHIRHelpers_4_0_001 = new FHIRHelpers_4_0_001(context);
        SupplementalDataElementsFHIR4_2_0_000 = new SupplementalDataElementsFHIR4_2_0_000(context);
        MATGlobalCommonFunctionsFHIR4_6_1_000 = new MATGlobalCommonFunctionsFHIR4_6_1_000(context);

        __birth_date = new Lazy<CqlValueSet>(this.birth_date_Value(context));
        __Emergency_Department_Visit = new Lazy<CqlValueSet>(this.Emergency_Department_Visit_Value(context));
        __Encounter_Inpatient = new Lazy<CqlValueSet>(this.Encounter_Inpatient_Value(context));
        __Glucose_lab_test = new Lazy<CqlValueSet>(this.Glucose_lab_test_Value(context));
        __Hypoglycemics = new Lazy<CqlValueSet>(this.Hypoglycemics_Value(context));
        __Hypoglycemics_Severe_Hypoglycemia = new Lazy<CqlValueSet>(this.Hypoglycemics_Severe_Hypoglycemia_Value(context));
        __Observation_Services = new Lazy<CqlValueSet>(this.Observation_Services_Value(context));
        __Birth_date = new Lazy<CqlCode>(this.Birth_date_Value(context));
        __LOINC = new Lazy<CqlCode[]>(this.LOINC_Value(context));
        __Measurement_Period = new Lazy<CqlInterval<CqlDateTime>>(this.Measurement_Period_Value(context));
        __Patient = new Lazy<Patient>(this.Patient_Value(context));
        __SDE_Payer = new Lazy<IEnumerable<Tuples.Tuple_CFQHSgYJOXjAOCKdWLdZNNHDG>>(this.SDE_Payer_Value(context));
        __SDE_Race = new Lazy<IEnumerable<Coding>>(this.SDE_Race_Value(context));
        __SDE_Sex = new Lazy<CqlCode>(this.SDE_Sex_Value(context));
        __Inpatient_Encounter_During_Measurement_Period = new Lazy<IEnumerable<Encounter>>(this.Inpatient_Encounter_During_Measurement_Period_Value(context));
        __Qualifying_Encounter = new Lazy<IEnumerable<Encounter>>(this.Qualifying_Encounter_Value(context));
        __Hypoglycemic_Medication_Administration = new Lazy<IEnumerable<MedicationAdministration>>(this.Hypoglycemic_Medication_Administration_Value(context));
        __Qualifying_Encounter_with_Hypoglycemic_Medication_Administration = new Lazy<IEnumerable<Encounter>>(this.Qualifying_Encounter_with_Hypoglycemic_Medication_Administration_Value(context));
        __Initial_Population = new Lazy<IEnumerable<Encounter>>(this.Initial_Population_Value(context));
        __Denominator = new Lazy<IEnumerable<Encounter>>(this.Denominator_Value(context));
        __Severe_Hypoglycemic_Harm_Event = new Lazy<IEnumerable<Encounter>>(this.Severe_Hypoglycemic_Harm_Event_Value(context));
        __Numerator = new Lazy<IEnumerable<Encounter>>(this.Numerator_Value(context));
        __SDE_Ethnicity = new Lazy<IEnumerable<Coding>>(this.SDE_Ethnicity_Value(context));
    }
    #region Dependencies

    public FHIRHelpers_4_0_001 FHIRHelpers_4_0_001 { get; }
    public SupplementalDataElementsFHIR4_2_0_000 SupplementalDataElementsFHIR4_2_0_000 { get; }
    public MATGlobalCommonFunctionsFHIR4_6_1_000 MATGlobalCommonFunctionsFHIR4_6_1_000 { get; }

    #endregion

	private CqlValueSet birth_date_Value(CqlContext context) => 
		new CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.560.100.4", null);

    [CqlDeclaration("birth date")]
    [CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.560.100.4")]
	public CqlValueSet birth_date() => 
		__birth_date?.Value;

	private CqlValueSet Emergency_Department_Visit_Value(CqlContext context) => 
		new CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.117.1.7.1.292", null);

    [CqlDeclaration("Emergency Department Visit")]
    [CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.117.1.7.1.292")]
	public CqlValueSet Emergency_Department_Visit() => 
		__Emergency_Department_Visit?.Value;

	private CqlValueSet Encounter_Inpatient_Value(CqlContext context) => 
		new CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.666.5.307", null);

    [CqlDeclaration("Encounter Inpatient")]
    [CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.666.5.307")]
	public CqlValueSet Encounter_Inpatient() => 
		__Encounter_Inpatient?.Value;

	private CqlValueSet Glucose_lab_test_Value(CqlContext context) => 
		new CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1045.134", null);

    [CqlDeclaration("Glucose lab test")]
    [CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1045.134")]
	public CqlValueSet Glucose_lab_test() => 
		__Glucose_lab_test?.Value;

	private CqlValueSet Hypoglycemics_Value(CqlContext context) => 
		new CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1179.3", null);

    [CqlDeclaration("Hypoglycemics")]
    [CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1179.3")]
	public CqlValueSet Hypoglycemics() => 
		__Hypoglycemics?.Value;

	private CqlValueSet Hypoglycemics_Severe_Hypoglycemia_Value(CqlContext context) => 
		new CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1196.393", null);

    [CqlDeclaration("Hypoglycemics Severe Hypoglycemia")]
    [CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1196.393")]
	public CqlValueSet Hypoglycemics_Severe_Hypoglycemia() => 
		__Hypoglycemics_Severe_Hypoglycemia?.Value;

	private CqlValueSet Observation_Services_Value(CqlContext context) => 
		new CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1111.143", null);

    [CqlDeclaration("Observation Services")]
    [CqlValueSet("http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1111.143")]
	public CqlValueSet Observation_Services() => 
		__Observation_Services?.Value;

	private CqlCode Birth_date_Value(CqlContext context) => 
		new CqlCode("21112-8", "http://loinc.org", null, null);

    [CqlDeclaration("Birth date")]
	public CqlCode Birth_date() => 
		__Birth_date?.Value;

	private CqlCode[] LOINC_Value(CqlContext context)
	{
		var a_ = new CqlCode[]
		{
			new CqlCode("21112-8", "http://loinc.org", null, null),
		};

		return a_;
	}

    [CqlDeclaration("LOINC")]
	public CqlCode[] LOINC() => 
		__LOINC?.Value;

	private CqlInterval<CqlDateTime> Measurement_Period_Value(CqlContext context)
	{
		var a_ = context.Operators.ConvertIntegerToDecimal(default);
		var b_ = context.Operators.DateTime((int?)2019, (int?)1, (int?)1, (int?)0, (int?)0, (int?)0, (int?)0, a_);
		var d_ = context.Operators.DateTime((int?)2020, (int?)1, (int?)1, (int?)0, (int?)0, (int?)0, (int?)0, a_);
		var e_ = context.Operators.Interval(b_, d_, true, false);
		var f_ = context.ResolveParameter("HospitalHarmSevereHypoglycemiaFHIR-0.0.012", "Measurement Period", e_);

		return (CqlInterval<CqlDateTime>)f_;
	}

    [CqlDeclaration("Measurement Period")]
	public CqlInterval<CqlDateTime> Measurement_Period() => 
		__Measurement_Period?.Value;

	private Patient Patient_Value(CqlContext context)
	{
		var a_ = context.Operators.RetrieveByValueSet<Patient>(null, null);
		var b_ = context.Operators.SingleOrNull<Patient>(a_);

		return b_;
	}

    [CqlDeclaration("Patient")]
	public Patient Patient() => 
		__Patient?.Value;

	private IEnumerable<Tuples.Tuple_CFQHSgYJOXjAOCKdWLdZNNHDG> SDE_Payer_Value(CqlContext context)
	{
		var a_ = SupplementalDataElementsFHIR4_2_0_000.SDE_Payer();

		return a_;
	}

    [CqlDeclaration("SDE Payer")]
	public IEnumerable<Tuples.Tuple_CFQHSgYJOXjAOCKdWLdZNNHDG> SDE_Payer() => 
		__SDE_Payer?.Value;

	private IEnumerable<Coding> SDE_Race_Value(CqlContext context)
	{
		var a_ = SupplementalDataElementsFHIR4_2_0_000.SDE_Race();

		return a_;
	}

    [CqlDeclaration("SDE Race")]
	public IEnumerable<Coding> SDE_Race() => 
		__SDE_Race?.Value;

	private CqlCode SDE_Sex_Value(CqlContext context)
	{
		var a_ = SupplementalDataElementsFHIR4_2_0_000.SDE_Sex();

		return a_;
	}

    [CqlDeclaration("SDE Sex")]
	public CqlCode SDE_Sex() => 
		__SDE_Sex?.Value;

	private IEnumerable<Encounter> Inpatient_Encounter_During_Measurement_Period_Value(CqlContext context)
	{
		var a_ = this.Encounter_Inpatient();
		var b_ = context.Operators.RetrieveByValueSet<Encounter>(a_, null);
		bool? c_(Encounter EncounterInpatient)
		{
			var e_ = context.Operators.Convert<string>(EncounterInpatient?.StatusElement);
			var f_ = context.Operators.Equal(e_, "finished");
			var g_ = FHIRHelpers_4_0_001.ToInterval(EncounterInpatient?.Period);
			var h_ = context.Operators.End(g_);
			var i_ = this.Measurement_Period();
			var j_ = context.Operators.ElementInInterval<CqlDateTime>(h_, i_, null);
			var k_ = context.Operators.And(f_, j_);

			return k_;
		};
		var d_ = context.Operators.WhereOrNull<Encounter>(b_, c_);

		return d_;
	}

    [CqlDeclaration("Inpatient Encounter During Measurement Period")]
	public IEnumerable<Encounter> Inpatient_Encounter_During_Measurement_Period() => 
		__Inpatient_Encounter_During_Measurement_Period?.Value;

	private IEnumerable<Encounter> Qualifying_Encounter_Value(CqlContext context)
	{
		var a_ = this.Inpatient_Encounter_During_Measurement_Period();
		bool? b_(Encounter InpatientEncounter)
		{
			var d_ = this.Patient();
			var e_ = context.Operators.ConvertStringToDateTime(d_?.BirthDateElement?.Value);
			var f_ = MATGlobalCommonFunctionsFHIR4_6_1_000.HospitalizationWithObservation(InpatientEncounter);
			var g_ = context.Operators.Start(f_);
			var h_ = context.Operators.CalculateAgeAt(e_, g_, "year");
			var i_ = context.Operators.GreaterOrEqual(h_, (int?)18);

			return i_;
		};
		var c_ = context.Operators.WhereOrNull<Encounter>(a_, b_);

		return c_;
	}

    [CqlDeclaration("Qualifying Encounter")]
	public IEnumerable<Encounter> Qualifying_Encounter() => 
		__Qualifying_Encounter?.Value;

	private IEnumerable<MedicationAdministration> Hypoglycemic_Medication_Administration_Value(CqlContext context)
	{
		var a_ = this.Hypoglycemics_Severe_Hypoglycemia();
		var b_ = context.Operators.RetrieveByValueSet<MedicationAdministration>(a_, null);
		var d_ = context.Operators.RetrieveByValueSet<MedicationAdministration>(a_, null);
		var e_ = context.Operators.ListUnion<MedicationAdministration>(b_, d_);
		bool? f_(MedicationAdministration HypoMedication)
		{
			var h_ = context.Operators.Convert<string>(HypoMedication?.StatusElement);
			var i_ = context.Operators.Equal(h_, "completed");
			var k_ = context.Operators.Equal(h_, "not-done");
			var l_ = context.Operators.Not(k_);
			var m_ = context.Operators.And(i_, l_);

			return m_;
		};
		var g_ = context.Operators.WhereOrNull<MedicationAdministration>(e_, f_);

		return g_;
	}

    [CqlDeclaration("Hypoglycemic Medication Administration")]
	public IEnumerable<MedicationAdministration> Hypoglycemic_Medication_Administration() => 
		__Hypoglycemic_Medication_Administration?.Value;

	private IEnumerable<Encounter> Qualifying_Encounter_with_Hypoglycemic_Medication_Administration_Value(CqlContext context)
	{
		var a_ = this.Qualifying_Encounter();
		IEnumerable<Encounter> b_(Encounter QualifyingEncounter)
		{
			var d_ = this.Hypoglycemic_Medication_Administration();
			bool? e_(MedicationAdministration HypoglycemicMedication)
			{
				var i_ = MATGlobalCommonFunctionsFHIR4_6_1_000.Normalize_Interval(HypoglycemicMedication?.Effective);
				var j_ = context.Operators.Start(i_);
				var k_ = MATGlobalCommonFunctionsFHIR4_6_1_000.HospitalizationWithObservation(QualifyingEncounter);
				var l_ = context.Operators.ElementInInterval<CqlDateTime>(j_, k_, null);

				return l_;
			};
			var f_ = context.Operators.WhereOrNull<MedicationAdministration>(d_, e_);
			Encounter g_(MedicationAdministration HypoglycemicMedication) => 
				QualifyingEncounter;
			var h_ = context.Operators.SelectOrNull<MedicationAdministration, Encounter>(f_, g_);

			return h_;
		};
		var c_ = context.Operators.SelectManyOrNull<Encounter, Encounter>(a_, b_);

		return c_;
	}

    [CqlDeclaration("Qualifying Encounter with Hypoglycemic Medication Administration")]
	public IEnumerable<Encounter> Qualifying_Encounter_with_Hypoglycemic_Medication_Administration() => 
		__Qualifying_Encounter_with_Hypoglycemic_Medication_Administration?.Value;

	private IEnumerable<Encounter> Initial_Population_Value(CqlContext context)
	{
		var a_ = this.Qualifying_Encounter_with_Hypoglycemic_Medication_Administration();

		return a_;
	}

    [CqlDeclaration("Initial Population")]
	public IEnumerable<Encounter> Initial_Population() => 
		__Initial_Population?.Value;

	private IEnumerable<Encounter> Denominator_Value(CqlContext context)
	{
		var a_ = this.Initial_Population();

		return a_;
	}

    [CqlDeclaration("Denominator")]
	public IEnumerable<Encounter> Denominator() => 
		__Denominator?.Value;

	private IEnumerable<Encounter> Severe_Hypoglycemic_Harm_Event_Value(CqlContext context)
	{
		var a_ = this.Denominator();
		bool? b_(Encounter QualifyingEncounter)
		{
			var d_ = this.Glucose_lab_test();
			var e_ = context.Operators.RetrieveByValueSet<Observation>(d_, null);
			IEnumerable<Observation> f_(Observation BloodGlucoseLab)
			{
				var r_ = this.Hypoglycemic_Medication_Administration();
				bool? s_(MedicationAdministration HypoglycemicMeds)
				{
					var w_ = MATGlobalCommonFunctionsFHIR4_6_1_000.Normalize_Interval(HypoglycemicMeds?.Effective);
					var x_ = context.Operators.Start(w_);
					var y_ = MATGlobalCommonFunctionsFHIR4_6_1_000.Normalize_Interval(BloodGlucoseLab?.Effective);
					var z_ = context.Operators.Start(y_);
					var aa_ = context.Operators.Quantity(24m, "hours");
					var ab_ = context.Operators.Subtract(z_, aa_);
					var ad_ = context.Operators.Start(y_);
					var ae_ = context.Operators.Interval(ab_, ad_, true, true);
					var af_ = context.Operators.ElementInInterval<CqlDateTime>(x_, ae_, null);
					var ah_ = context.Operators.Start(y_);
					var ai_ = context.Operators.Not((bool?)(ah_ is null));
					var aj_ = context.Operators.And(af_, ai_);
					var ak_ = context.Operators.Convert<string>(BloodGlucoseLab?.StatusElement);
					var al_ = context.Operators.Equal(ak_, "final");
					var am_ = context.Operators.And(aj_, al_);
					var ao_ = context.Operators.Equal(ak_, "cancelled");
					var ap_ = context.Operators.Not(ao_);
					var aq_ = context.Operators.And(am_, ap_);
					var as_ = context.Operators.Start(w_);
					var at_ = MATGlobalCommonFunctionsFHIR4_6_1_000.HospitalizationWithObservation(QualifyingEncounter);
					var au_ = context.Operators.ElementInInterval<CqlDateTime>(as_, at_, null);
					var av_ = context.Operators.And(aq_, au_);

					return av_;
				};
				var t_ = context.Operators.WhereOrNull<MedicationAdministration>(r_, s_);
				Observation u_(MedicationAdministration HypoglycemicMeds) => 
					BloodGlucoseLab;
				var v_ = context.Operators.SelectOrNull<MedicationAdministration, Observation>(t_, u_);

				return v_;
			};
			var g_ = context.Operators.SelectManyOrNull<Observation, Observation>(e_, f_);
			var i_ = context.Operators.RetrieveByValueSet<Observation>(d_, null);
			IEnumerable<Observation> j_(Observation BloodGlucoseLab)
			{
				var aw_ = this.Hypoglycemic_Medication_Administration();
				bool? ax_(MedicationAdministration HypoglycemicMeds)
				{
					var bb_ = MATGlobalCommonFunctionsFHIR4_6_1_000.Normalize_Interval(HypoglycemicMeds?.Effective);
					var bc_ = context.Operators.Start(bb_);
					var bd_ = MATGlobalCommonFunctionsFHIR4_6_1_000.Normalize_Interval(BloodGlucoseLab?.Effective);
					var be_ = context.Operators.Start(bd_);
					var bf_ = context.Operators.Quantity(24m, "hours");
					var bg_ = context.Operators.Subtract(be_, bf_);
					var bi_ = context.Operators.Start(bd_);
					var bj_ = context.Operators.Interval(bg_, bi_, true, true);
					var bk_ = context.Operators.ElementInInterval<CqlDateTime>(bc_, bj_, null);
					var bm_ = context.Operators.Start(bd_);
					var bn_ = context.Operators.Not((bool?)(bm_ is null));
					var bo_ = context.Operators.And(bk_, bn_);
					var bp_ = context.Operators.Convert<string>(BloodGlucoseLab?.StatusElement);
					var bq_ = context.Operators.Equal(bp_, "final");
					var br_ = context.Operators.And(bo_, bq_);
					var bt_ = context.Operators.Equal(bp_, "cancelled");
					var bu_ = context.Operators.Not(bt_);
					var bv_ = context.Operators.And(br_, bu_);
					var bx_ = context.Operators.Start(bb_);
					var by_ = MATGlobalCommonFunctionsFHIR4_6_1_000.HospitalizationWithObservation(QualifyingEncounter);
					var bz_ = context.Operators.ElementInInterval<CqlDateTime>(bx_, by_, null);
					var ca_ = context.Operators.And(bv_, bz_);

					return ca_;
				};
				var ay_ = context.Operators.WhereOrNull<MedicationAdministration>(aw_, ax_);
				Observation az_(MedicationAdministration HypoglycemicMeds) => 
					BloodGlucoseLab;
				var ba_ = context.Operators.SelectOrNull<MedicationAdministration, Observation>(ay_, az_);

				return ba_;
			};
			var k_ = context.Operators.SelectManyOrNull<Observation, Observation>(i_, j_);
			IEnumerable<Observation> l_(Observation BloodGlucoseLab)
			{
				var cb_ = this.Glucose_lab_test();
				var cc_ = context.Operators.RetrieveByValueSet<Observation>(cb_, null);
				bool? cd_(Observation FollowupBloodGlucoseLab)
				{
					var ch_ = MATGlobalCommonFunctionsFHIR4_6_1_000.Normalize_Interval(FollowupBloodGlucoseLab?.Effective);
					var ci_ = context.Operators.Start(ch_);
					var cj_ = MATGlobalCommonFunctionsFHIR4_6_1_000.HospitalizationWithObservation(QualifyingEncounter);
					var ck_ = context.Operators.ElementInInterval<CqlDateTime>(ci_, cj_, null);
					var cm_ = context.Operators.Start(ch_);
					var cn_ = MATGlobalCommonFunctionsFHIR4_6_1_000.Normalize_Interval(BloodGlucoseLab?.Effective);
					var co_ = context.Operators.Start(cn_);
					var cq_ = context.Operators.Start(cn_);
					var cr_ = context.Operators.Quantity(5m, "minutes");
					var cs_ = context.Operators.Add(cq_, cr_);
					var ct_ = context.Operators.Interval(co_, cs_, false, true);
					var cu_ = context.Operators.ElementInInterval<CqlDateTime>(cm_, ct_, null);
					var cw_ = context.Operators.Start(cn_);
					var cx_ = context.Operators.Not((bool?)(cw_ is null));
					var cy_ = context.Operators.And(cu_, cx_);
					var cz_ = context.Operators.And(ck_, cy_);
					var da_ = context.Operators.Convert<string>(FollowupBloodGlucoseLab?.StatusElement);
					var db_ = context.Operators.Equal(da_, "final");
					var dc_ = context.Operators.And(cz_, db_);
					var de_ = context.Operators.Equal(da_, "cancelled");
					var df_ = context.Operators.Not(de_);
					var dg_ = context.Operators.And(dc_, df_);
					var dh_ = FHIRHelpers_4_0_001.ToQuantity((FollowupBloodGlucoseLab?.Value as Quantity));
					var di_ = context.Operators.Quantity(80m, "mg/dL");
					var dj_ = context.Operators.Greater(dh_, di_);
					var dk_ = context.Operators.And(dg_, dj_);

					return dk_;
				};
				var ce_ = context.Operators.WhereOrNull<Observation>(cc_, cd_);
				Observation cf_(Observation FollowupBloodGlucoseLab) => 
					BloodGlucoseLab;
				var cg_ = context.Operators.SelectOrNull<Observation, Observation>(ce_, cf_);

				return cg_;
			};
			var m_ = context.Operators.SelectManyOrNull<Observation, Observation>(k_, l_);
			var n_ = context.Operators.ListExcept<Observation>(g_, m_);
			bool? o_(Observation BloodGlucoseLab)
			{
				var dl_ = MATGlobalCommonFunctionsFHIR4_6_1_000.Normalize_Interval(BloodGlucoseLab?.Effective);
				var dm_ = context.Operators.Start(dl_);
				var dn_ = MATGlobalCommonFunctionsFHIR4_6_1_000.HospitalizationWithObservation(QualifyingEncounter);
				var do_ = context.Operators.ElementInInterval<CqlDateTime>(dm_, dn_, null);
				var dp_ = FHIRHelpers_4_0_001.ToQuantity((BloodGlucoseLab?.Value as Quantity));
				var dq_ = context.Operators.Quantity(40m, "mg/dL");
				var dr_ = context.Operators.Less(dp_, dq_);
				var ds_ = context.Operators.And(do_, dr_);

				return ds_;
			};
			var p_ = context.Operators.WhereOrNull<Observation>(n_, o_);
			var q_ = context.Operators.ExistsInList<Observation>(p_);

			return q_;
		};
		var c_ = context.Operators.WhereOrNull<Encounter>(a_, b_);

		return c_;
	}

    [CqlDeclaration("Severe Hypoglycemic Harm Event")]
	public IEnumerable<Encounter> Severe_Hypoglycemic_Harm_Event() => 
		__Severe_Hypoglycemic_Harm_Event?.Value;

	private IEnumerable<Encounter> Numerator_Value(CqlContext context)
	{
		var a_ = this.Severe_Hypoglycemic_Harm_Event();

		return a_;
	}

    [CqlDeclaration("Numerator")]
	public IEnumerable<Encounter> Numerator() => 
		__Numerator?.Value;

	private IEnumerable<Coding> SDE_Ethnicity_Value(CqlContext context)
	{
		var a_ = SupplementalDataElementsFHIR4_2_0_000.SDE_Ethnicity();

		return a_;
	}

    [CqlDeclaration("SDE Ethnicity")]
	public IEnumerable<Coding> SDE_Ethnicity() => 
		__SDE_Ethnicity?.Value;

}