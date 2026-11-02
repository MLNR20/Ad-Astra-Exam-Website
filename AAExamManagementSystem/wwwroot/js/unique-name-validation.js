// Live check for [UniqueActiveName] fields: asks the server whether another active record already uses the name.
// The name is sent along with the record being edited (the form's Id field) and whether the form saves it as
// active, since an inactive record may keep any name.
(function ($) {
    if (!$ || !$.validator || !$.validator.unobtrusive) {
        return;
    }

    $.validator.unobtrusive.adapters.add('uniquename', ['url', 'kind'], function (options) {
        var form = $(options.form);
        options.rules['remote'] = {
            url: options.params.url,
            type: 'get',
            data: {
                kind: options.params.kind,
                name: function () { return $(options.element).val(); },
                excludeId: function () { return form.find('input[name="Id"]').val() || ''; },
                active: function () {
                    var toggle = form.find('input[type="checkbox"][name$="IsActive"]');
                    return toggle.length ? toggle.is(':checked') : true;
                }
            }
        };
        options.messages['remote'] = options.message;
    });
}(window.jQuery));
