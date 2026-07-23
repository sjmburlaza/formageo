"""Main FormaGeo Studio QGIS plugin class."""


class FormaGeoStudioPlugin:
    """QGIS plugin lifecycle implementation."""

    def __init__(self, iface):
        self.iface = iface

    def initGui(self):
        """Create menus, toolbar buttons and processing providers."""
        pass

    def unload(self):
        """Remove plugin UI elements and release resources."""
        pass