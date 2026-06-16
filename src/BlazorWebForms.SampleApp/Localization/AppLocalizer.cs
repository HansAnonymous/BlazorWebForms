using System.Globalization;

namespace BlazorWebForms.SampleApp.Localization;

public sealed class AppLocalizer
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Strings
        = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Layout.Title"] = "BlazorWebForms Sample",
                ["Layout.LogOut"] = "Log out",
                ["Layout.DevLogin"] = "Dev login",
                ["Layout.Docs"] = "Docs",
                ["Layout.Culture"] = "Language",
                ["Layout.ApplyCulture"] = "Apply",
                ["Layout.SkipToMain"] = "Skip to main content",
                ["Layout.UserAnonymous"] = "Anonymous",
                ["Nav.Workspace"] = "Workspace",
                ["Nav.Forms"] = "Forms",
                ["Nav.Overview"] = "Overview",
                ["Nav.Admin"] = "Admin (server query)",
                ["Nav.Builder"] = "Builder + publish",
                ["Nav.Published"] = "Published + upload",
                ["Common.Loading"] = "Loading...",
                ["Common.Status"] = "Status",
                ["Common.Select"] = "Select...",
                ["Published.Loading"] = "Loading published form...",
                ["Published.PublishedForm"] = "Published form",
                ["Published.Workflow"] = "Workflow",
                ["Published.Approvers"] = "Approvers",
                ["Published.ApproverName"] = "Approver name",
                ["Published.ApproverEmail"] = "Approver email",
                ["Published.AddApprover"] = "Add approver",
                ["Published.Submit"] = "Submit",
                ["Published.AttachedFiles"] = "Attached files: {0}",
                ["Entry.Loading"] = "Loading entry...",
                ["Entry.NotFound"] = "Entry not available.",
                ["Entry.AccessDenied"] = "Current user cannot view this entry.",
                ["Entry.GeneratePdf"] = "Generate PDF",
                ["Entry.GeneratingPdf"] = "Generating PDF...",
                ["Entry.PdfDownloadStarted"] = "PDF download started.",
                ["Entry.PdfExportFailed"] = "PDF export failed: {0}",
                ["Admin.Loading"] = "Loading admin dashboard...",
                ["Admin.Forms"] = "Forms",
                ["Admin.Entries"] = "Entries",
                ["Admin.AllStatuses"] = "All statuses",
                ["Admin.SearchPlaceholder"] = "Search submitter or indexed values",
                ["Admin.IndexedFieldId"] = "Indexed field id (optional)",
                ["Admin.IndexedFieldValue"] = "Indexed field value (optional)",
                ["Admin.NoEntries"] = "No entries match the current filter set.",
                ["State.Loading"] = "Loading",
                ["State.RecoverableError"] = "Recoverable error",
                ["State.Retry"] = "Retry",
                ["State.SaveTimedOut"] = "Save draft timed out. Retry in a few seconds.",
                ["State.PublishTimedOut"] = "Publish timed out. Retry, or save draft and publish again.",
                ["State.SubmitTimedOut"] = "Submit timed out. Retry submit to attempt again.",
                ["State.RetryTimedOut"] = "Retry timed out. Save draft and try again.",
                ["State.RetrySubmit"] = "Retry submit"
            },
            ["fr"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Layout.Title"] = "Exemple BlazorWebForms",
                ["Layout.LogOut"] = "Se deconnecter",
                ["Layout.DevLogin"] = "Connexion dev",
                ["Layout.Docs"] = "Documentation",
                ["Layout.Culture"] = "Langue",
                ["Layout.ApplyCulture"] = "Appliquer",
                ["Layout.SkipToMain"] = "Aller au contenu principal",
                ["Layout.UserAnonymous"] = "Anonyme",
                ["Nav.Workspace"] = "Espace de travail",
                ["Nav.Forms"] = "Formulaires",
                ["Nav.Overview"] = "Apercu",
                ["Nav.Admin"] = "Admin (requete serveur)",
                ["Nav.Builder"] = "Constructeur + publier",
                ["Nav.Published"] = "Publie + televersement",
                ["Common.Loading"] = "Chargement...",
                ["Common.Status"] = "Statut",
                ["Common.Select"] = "Selectionner...",
                ["Published.Loading"] = "Chargement du formulaire publie...",
                ["Published.PublishedForm"] = "Formulaire publie",
                ["Published.Workflow"] = "Workflow",
                ["Published.Approvers"] = "Approbateurs",
                ["Published.ApproverName"] = "Nom de l'approbateur",
                ["Published.ApproverEmail"] = "E-mail de l'approbateur",
                ["Published.AddApprover"] = "Ajouter un approbateur",
                ["Published.Submit"] = "Soumettre",
                ["Published.AttachedFiles"] = "Fichiers joints : {0}",
                ["Entry.Loading"] = "Chargement de l'entree...",
                ["Entry.NotFound"] = "Entree indisponible.",
                ["Entry.AccessDenied"] = "L'utilisateur actuel ne peut pas afficher cette entree.",
                ["Entry.GeneratePdf"] = "Generer le PDF",
                ["Entry.GeneratingPdf"] = "Generation du PDF...",
                ["Entry.PdfDownloadStarted"] = "Telechargement du PDF demarre.",
                ["Entry.PdfExportFailed"] = "Echec de l'export PDF : {0}",
                ["Admin.Loading"] = "Chargement du tableau d'administration...",
                ["Admin.Forms"] = "Formulaires",
                ["Admin.Entries"] = "Entrees",
                ["Admin.AllStatuses"] = "Tous les statuts",
                ["Admin.SearchPlaceholder"] = "Rechercher un soumetteur ou des valeurs indexees",
                ["Admin.IndexedFieldId"] = "Id du champ indexe (optionnel)",
                ["Admin.IndexedFieldValue"] = "Valeur du champ indexe (optionnel)",
                ["Admin.NoEntries"] = "Aucune entree ne correspond aux filtres.",
                ["State.Loading"] = "Chargement",
                ["State.RecoverableError"] = "Erreur recuperable",
                ["State.Retry"] = "Reessayer",
                ["State.SaveTimedOut"] = "L'enregistrement du brouillon a expire. Reessayez dans quelques secondes.",
                ["State.PublishTimedOut"] = "La publication a expire. Reessayez ou enregistrez le brouillon puis republiez.",
                ["State.SubmitTimedOut"] = "La soumission a expire. Reessayez la soumission.",
                ["State.RetryTimedOut"] = "La nouvelle tentative a expire. Enregistrez un brouillon puis reessayez.",
                ["State.RetrySubmit"] = "Reessayer la soumission"
            },
            ["es"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Layout.Title"] = "Ejemplo BlazorWebForms",
                ["Layout.LogOut"] = "Cerrar sesion",
                ["Layout.DevLogin"] = "Inicio dev",
                ["Layout.Docs"] = "Documentacion",
                ["Layout.Culture"] = "Idioma",
                ["Layout.ApplyCulture"] = "Aplicar",
                ["Layout.SkipToMain"] = "Saltar al contenido principal",
                ["Layout.UserAnonymous"] = "Anonimo",
                ["Nav.Workspace"] = "Espacio de trabajo",
                ["Nav.Forms"] = "Formularios",
                ["Nav.Overview"] = "Resumen",
                ["Nav.Admin"] = "Admin (consulta servidor)",
                ["Nav.Builder"] = "Constructor + publicar",
                ["Nav.Published"] = "Publicado + carga",
                ["Common.Loading"] = "Cargando...",
                ["Common.Status"] = "Estado",
                ["Common.Select"] = "Seleccionar...",
                ["Published.Loading"] = "Cargando formulario publicado...",
                ["Published.PublishedForm"] = "Formulario publicado",
                ["Published.Workflow"] = "Flujo",
                ["Published.Approvers"] = "Aprobadores",
                ["Published.ApproverName"] = "Nombre del aprobador",
                ["Published.ApproverEmail"] = "Correo del aprobador",
                ["Published.AddApprover"] = "Agregar aprobador",
                ["Published.Submit"] = "Enviar",
                ["Published.AttachedFiles"] = "Archivos adjuntos: {0}",
                ["Entry.Loading"] = "Cargando entrada...",
                ["Entry.NotFound"] = "Entrada no disponible.",
                ["Entry.AccessDenied"] = "El usuario actual no puede ver esta entrada.",
                ["Entry.GeneratePdf"] = "Generar PDF",
                ["Entry.GeneratingPdf"] = "Generando PDF...",
                ["Entry.PdfDownloadStarted"] = "Descarga de PDF iniciada.",
                ["Entry.PdfExportFailed"] = "Error al exportar PDF: {0}",
                ["Admin.Loading"] = "Cargando panel de administracion...",
                ["Admin.Forms"] = "Formularios",
                ["Admin.Entries"] = "Entradas",
                ["Admin.AllStatuses"] = "Todos los estados",
                ["Admin.SearchPlaceholder"] = "Buscar remitente o valores indexados",
                ["Admin.IndexedFieldId"] = "Id de campo indexado (opcional)",
                ["Admin.IndexedFieldValue"] = "Valor de campo indexado (opcional)",
                ["Admin.NoEntries"] = "No hay entradas para los filtros actuales.",
                ["State.Loading"] = "Cargando",
                ["State.RecoverableError"] = "Error recuperable",
                ["State.Retry"] = "Reintentar",
                ["State.SaveTimedOut"] = "Se agoto el tiempo al guardar borrador. Reintente en unos segundos.",
                ["State.PublishTimedOut"] = "Se agoto el tiempo al publicar. Reintente, o guarde borrador y publique de nuevo.",
                ["State.SubmitTimedOut"] = "Se agoto el tiempo al enviar. Reintente el envio.",
                ["State.RetryTimedOut"] = "Se agoto el tiempo del reintento. Guarde borrador e intente nuevamente.",
                ["State.RetrySubmit"] = "Reintentar envio"
            }
        };

    public static readonly string[] SupportedCultures = ["en-US", "fr-FR", "es-ES", "ar-SA"];

    public string Get(string key)
    {
        var culture = CultureInfo.CurrentUICulture;
        foreach (var candidate in GetCandidateLanguages(culture))
        {
            if (Strings.TryGetValue(candidate, out var map) && map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return key;
    }

    public string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    private static IEnumerable<string> GetCandidateLanguages(CultureInfo culture)
    {
        if (!string.IsNullOrWhiteSpace(culture.Name))
        {
            yield return culture.Name;
        }

        if (!string.IsNullOrWhiteSpace(culture.TwoLetterISOLanguageName))
        {
            yield return culture.TwoLetterISOLanguageName;
        }

        yield return "en";
    }
}
