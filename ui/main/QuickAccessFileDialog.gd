extends FileDialog
class_name QAFileDialog  #A file dialog which supports quick access functionality.

export var quick_access_category = "Quick Access"
onready var q = get_node("%QuickAccess")

func _ready():
	hook_quick_access(self)
	connect("file_selected",self,"_on_file_selected")

func hook_quick_access(dlg:FileDialog):
	#Find the hbox, we're about to get medieval
	for o in dlg.get_children():
		if not o is VBoxContainer:  continue
		var h = o.get_child(0)  #Should be the top hbox.
		var p = preload("res://ui/main/QuickAccessButton.tscn").instance()
		h.add_child(p)
		p.owner = self
		p.connect("pressed", self, "_on_quick_access_pressed", [dlg])


func _on_quick_access_pressed(caller:FileDialog):
	q.last_window = caller
	q.refresh(q.MRUDS)
	q.popup()



func _on_file_selected(path):
	#Add MRUD to the quick dialog
	q.add_mrud(path.get_base_dir())
	q.clean(q.MRUDS)
	q.save()
