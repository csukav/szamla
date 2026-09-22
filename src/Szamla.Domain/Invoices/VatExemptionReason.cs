namespace Szamla.Domain.Invoices;

/// <summary>
/// ÁFA-mentesség/kivétel esetei, a projektspecifikációban megnevezett kategóriák szerint (AAM,
/// TAM, EUE, EUFAD37, belföldi fordított adózás, területi hatályon kívüliség).
///
/// A NAV Online Számla XSD-jében szereplő pontos mezőnevek és kódlisták (a séma nem egyetlen
/// "vatExemptionReason" felsorolást használ, hanem külön choice-ágakat: vatExemption,
/// vatOutOfScope, vatDomesticReverseCharge stb., mindegyik saját kötelező mezőkkel, pl. jogszabályi
/// hivatkozás szövege) az 5. fázisban, a hivatalos séma alapján ellenőrizendők és a NAV-beküldési
/// modellben leképezendők. Ez a felsorolás a Domain-rétegbeli ÁFA-számításhoz (nulla fizetendő
/// adó felismerése) elegendő, de NAV-beküldésre önmagában nem tekinthető véglegesnek.
/// </summary>
public enum VatExemptionReason
{
    /// <summary>Alanyi adómentesség (AAM).</summary>
    SubjectExempt,

    /// <summary>Tárgyi adómentesség (TAM).</summary>
    ObjectExempt,

    /// <summary>Közösségen belüli, adómentes termékértékesítés (EUE).</summary>
    IntraCommunitySupply,

    /// <summary>Közösségen belüli, fordított adózású szolgáltatásnyújtás (EUFAD37).</summary>
    IntraCommunityReverseCharge,

    /// <summary>Belföldi fordított adózás.</summary>
    DomesticReverseCharge,

    /// <summary>Az Áfa törvény területi hatályán kívüli termékértékesítés/szolgáltatás (pl. harmadik országba történő export).</summary>
    OutsideVatScope,
}
