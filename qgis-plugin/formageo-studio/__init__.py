"""FormaGeo Studio QGIS plugin entry point."""


def classFactory(iface):
    """Return the initialized FormaGeo Studio plugin."""
    from .plugin import FormaGeoStudioPlugin

    return FormaGeoStudioPlugin(iface)